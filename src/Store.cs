using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ClipShelf;

public sealed record CapturedClip(string Kind, string Text, string? Html, string? Rtf, byte[]? Image, byte[]? Thumb, string Hash, string? Source);

public sealed record StoredClip(long Id, string Kind, string Text, string? Html, string? Rtf, byte[]? Image);

/// <summary>Permanent clip history in %LOCALAPPDATA%\ClipShelf\history.db. Identical content is stored once and bumped to the top.</summary>
public sealed class Store : IDisposable
{
    readonly SqliteConnection _db;

    public Store(string folder)
    {
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder, "history.db") }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL");
        Exec(@"CREATE TABLE IF NOT EXISTS clips (
                 id      INTEGER PRIMARY KEY AUTOINCREMENT,
                 kind    TEXT NOT NULL,
                 text    TEXT NOT NULL,
                 html    TEXT,
                 rtf     TEXT,
                 image   BLOB,
                 thumb   BLOB,
                 hash    TEXT NOT NULL UNIQUE,
                 source  TEXT,
                 created INTEGER NOT NULL,
                 used    INTEGER NOT NULL,
                 pinned  INTEGER NOT NULL DEFAULT 0);
               CREATE INDEX IF NOT EXISTS clips_used ON clips(used DESC);");
    }

    static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public void Save(CapturedClip c) => Exec(
        @"INSERT INTO clips (kind, text, html, rtf, image, thumb, hash, source, created, used)
          VALUES ($kind, $text, $html, $rtf, $image, $thumb, $hash, $source, $now, $now)
          ON CONFLICT(hash) DO UPDATE SET
            used   = excluded.used,
            source = COALESCE(excluded.source, clips.source),
            html   = COALESCE(excluded.html, clips.html),
            rtf    = COALESCE(excluded.rtf, clips.rtf)",
        ("$kind", c.Kind), ("$text", c.Text), ("$html", c.Html), ("$rtf", c.Rtf), ("$image", c.Image),
        ("$thumb", c.Thumb), ("$hash", c.Hash), ("$source", c.Source), ("$now", Now));

    /// <summary>Most recently used clips; every whitespace-separated term must match the text or source app.</summary>
    public List<ClipItem> Query(string search, bool pinnedOnly, int limit)
    {
        var sql = new StringBuilder("SELECT id, kind, substr(text, 1, 700), source, used, pinned, thumb, length(text) FROM clips WHERE 1=1");
        var args = new List<(string, object?)> { ("$limit", limit) };
        if (pinnedOnly) sql.Append(" AND pinned = 1");
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < terms.Length; i++)
        {
            sql.Append($" AND (text LIKE $q{i} ESCAPE '\\' OR source LIKE $q{i} ESCAPE '\\')");
            args.Add(($"$q{i}", "%" + terms[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"));
        }
        sql.Append(" ORDER BY used DESC LIMIT $limit");

        using var cmd = Command(sql.ToString(), args.ToArray());
        using var r = cmd.ExecuteReader();
        var items = new List<ClipItem>();
        while (r.Read())
        {
            items.Add(new ClipItem
            {
                Id = r.GetInt64(0),
                Kind = r.GetString(1),
                Text = r.GetString(2),
                Source = r.IsDBNull(3) ? null : r.GetString(3),
                Used = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(4)).LocalDateTime,
                Pinned = r.GetInt64(5) != 0,
                Thumb = r.IsDBNull(6) ? null : Imaging.Decode((byte[])r[6]),
                Length = r.GetInt64(7),
            });
        }
        return items;
    }

    public StoredClip? Get(long id)
    {
        using var cmd = Command("SELECT id, kind, text, html, rtf, image FROM clips WHERE id = $id", ("$id", id));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new StoredClip(r.GetInt64(0), r.GetString(1), r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            r.IsDBNull(5) ? null : (byte[])r[5]);
    }

    public long Count()
    {
        using var cmd = Command("SELECT COUNT(*) FROM clips");
        return (long)cmd.ExecuteScalar()!;
    }

    public void Touch(long id) => Exec("UPDATE clips SET used = $now WHERE id = $id", ("$now", Now), ("$id", id));
    public void SetPinned(long id, bool pinned) => Exec("UPDATE clips SET pinned = $p WHERE id = $id", ("$p", pinned ? 1 : 0), ("$id", id));
    public void Delete(long id) => Exec("DELETE FROM clips WHERE id = $id", ("$id", id));
    public void ClearUnpinned() => Exec("DELETE FROM clips WHERE pinned = 0");

    public long CountPinned()
    {
        using var cmd = Command("SELECT COUNT(*) FROM clips WHERE pinned = 1");
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>Deletes unpinned clips not used in the last <paramref name="days"/> days. Returns how many were removed.</summary>
    public int PurgeStale(int days)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();
        using var cmd = Command("DELETE FROM clips WHERE pinned = 0 AND used < $cutoff", ("$cutoff", cutoff));
        return cmd.ExecuteNonQuery();
    }

    void Exec(string sql, params (string, object?)[] args)
    {
        using var cmd = Command(sql, args);
        cmd.ExecuteNonQuery();
    }

    SqliteCommand Command(string sql, params (string Name, object? Value)[] args)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public void Dispose() => _db.Dispose();
}
