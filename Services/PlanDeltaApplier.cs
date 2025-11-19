using project.Services.AI;
using System.Text.RegularExpressions;
using System.Linq;

namespace project.Services
{
    public class PlanDeltaApplier
    {
        public string ApplyDeltaToItinerary(string currentItinerary, PlanDelta delta)
        {
            if (delta?.Changes == null || !delta.Changes.Any())
            {
                // Even if no per-day changes, we may still need to truncate days
                if (delta?.TruncateToDays is int t && t > 0)
                {
                    return TruncateItinerary(currentItinerary, t);
                }
                return currentItinerary;
            }

            var lines = currentItinerary.Split('\n').ToList();
            var processedDays = new System.Collections.Generic.HashSet<int>();

            foreach (var change in delta.Changes.OrderBy(c => c.Day))
            {
                var dayNumber = change.Day;
                if (!processedDays.Add(dayNumber)) continue;

                var dayPattern = new Regex($@"^Day\s+{dayNumber}\b", RegexOptions.IgnoreCase);
                var dayStart = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (dayPattern.IsMatch(lines[i]))
                    {
                        dayStart = i;
                        break;
                    }
                }

                if (dayStart < 0) continue;

                var dayEnd = FindDayEnd(lines, dayStart);
                ApplyChangesToDaySection(lines, dayStart, dayEnd, change);
            }

            var output = string.Join("\n", lines);

            if (delta.TruncateToDays is int truncate && truncate > 0)
            {
                output = TruncateItinerary(output, truncate);
            }

            return output;
        }

        private string TruncateItinerary(string itinerary, int daysToKeep)
        {
            if (daysToKeep <= 0) return itinerary;
            var lines = itinerary.Split('\n').ToList();
            var dayPattern = new Regex(@"^Day\s+(\d+)\b", RegexOptions.IgnoreCase);
            int cutIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var m = dayPattern.Match(lines[i]);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n == daysToKeep + 1)
                {
                    cutIndex = i; break;
                }
            }
            if (cutIndex >= 0)
            {
                lines = lines.Take(cutIndex).ToList();
            }
            return string.Join("\n", lines);
        }

        private void ApplyChangesToDaySection(System.Collections.Generic.List<string> lines, int start, int end, DayChange change)
        {
            if (change.Remove != null && change.Remove.Any())
            {
                for (int i = end; i >= start; i--)
                {
                    if (i >= lines.Count) continue;
                    var line = lines[i];
                    foreach (var toRemove in change.Remove)
                    {
                        if (line.Contains(toRemove, System.StringComparison.OrdinalIgnoreCase))
                        {
                            lines.RemoveAt(i);
                            end--;
                            break;
                        }
                    }
                }
            }

            if (change.AddMorning != null && change.AddMorning.Any())
            {
                var morningIdx = FindSectionIndex(lines, start, end, "Morning");
                if (morningIdx >= 0)
                {
                    int insertAt = morningIdx + 1;
                    foreach (var activity in change.AddMorning)
                    {
                        lines.Insert(insertAt, $"  - {activity}");
                        insertAt++;
                        end++;
                    }
                }
            }

            if (change.AddAfternoon != null && change.AddAfternoon.Any())
            {
                var afternoonIdx = FindSectionIndex(lines, start, end, "Afternoon");
                if (afternoonIdx >= 0)
                {
                    int insertAt = afternoonIdx + 1;
                    foreach (var activity in change.AddAfternoon)
                    {
                        lines.Insert(insertAt, $"  - {activity}");
                        insertAt++;
                        end++;
                    }
                }
            }

            if (change.AddEvening != null && change.AddEvening.Any())
            {
                var eveningIdx = FindSectionIndex(lines, start, end, "Evening");
                if (eveningIdx >= 0)
                {
                    int insertAt = eveningIdx + 1;
                    foreach (var activity in change.AddEvening)
                    {
                        lines.Insert(insertAt, $"  - {activity}");
                        insertAt++;
                        end++;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(change.Note) && end + 1 < lines.Count)
            {
                lines.Insert(end + 1, $"Note: {change.Note}");
            }
        }

        private int FindSectionIndex(System.Collections.Generic.List<string> lines, int start, int end, string section)
        {
            for (int i = start; i <= end && i < lines.Count; i++)
            {
                if (lines[i].Contains(section, System.StringComparison.OrdinalIgnoreCase) && lines[i].Contains(":"))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindDayEnd(System.Collections.Generic.List<string> lines, int dayStart)
        {
            var dayPattern = new Regex(@"^Day\s+\d+\b", RegexOptions.IgnoreCase);
            for (int i = dayStart + 1; i < lines.Count; i++)
            {
                if (dayPattern.IsMatch(lines[i]))
                {
                    return i - 1;
                }
            }
            return lines.Count - 1;
        }
    }
}
