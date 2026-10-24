using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Kimi and MiniMax only expose a balance, not today's usage. Estimate the daily spend from
// the drop in balance between refreshes: a top-up raises the baseline so it never shows a
// negative spend, and the counter restarts at midnight.
class DailyLog {
    class Entry {
        public string Date;
        public decimal Start;
        public decimal Last;
    }

    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    readonly string path;

    DailyLog(string path) { this.path = path; }

    public static DailyLog Load(string path) {
        var log = new DailyLog(path);
        try {
            if (!File.Exists(path)) return log;
            var data = Data.Json().Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path));
            if (data != null) foreach (var pair in data) if (pair.Value != null) log.entries[pair.Key] = pair.Value;
        } catch {
            // A corrupt file only loses today's estimate.
        }
        return log;
    }

    // Feeds one balance sample and returns the estimated spend since midnight.
    public decimal Sample(string id, decimal amount) {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        Entry entry;
        if (!entries.TryGetValue(id, out entry)) {
            entries[id] = new Entry { Date = today, Start = amount, Last = amount };
            Save();
            return 0;
        }
        if (entry.Date != today) {
            entry.Date = today;
            entry.Start = amount;
            entry.Last = amount;
            Save();
            return 0;
        }
        if (amount > entry.Last) entry.Start += amount - entry.Last;
        entry.Last = amount;
        decimal spend = entry.Start - amount;
        Save();
        return spend > 0 ? spend : 0;
    }

    void Save() {
        try {
            using (var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
                writer.Write(Data.Json().Serialize(entries));
        } catch {
            // The estimate is nice to have; never break a refresh over it.
        }
    }
}
