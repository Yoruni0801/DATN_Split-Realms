using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quoc.PuzzleQuest
{
    public sealed class PuzzleQuestMatchSummary
    {
        public PuzzleGemType Type { get; private set; }
        public int TotalMatched { get; private set; }
        public int LongestRun { get; private set; }
        public IReadOnlyCollection<Vector2Int> Cells { get; private set; }

        public PuzzleQuestMatchSummary(
            PuzzleGemType type,
            int totalMatched,
            int longestRun,
            IReadOnlyCollection<Vector2Int> cells)
        {
            Type = type;
            TotalMatched = totalMatched;
            LongestRun = longestRun;
            Cells = cells;
        }
    }

    public sealed class PuzzleQuestMatchResult
    {
        public static readonly PuzzleQuestMatchResult Empty = new PuzzleQuestMatchResult(
            new HashSet<Vector2Int>(),
            new List<PuzzleQuestMatchSummary>());

        public HashSet<Vector2Int> MatchedCells { get; private set; }
        public List<PuzzleQuestMatchSummary> Summaries { get; private set; }
        public bool HasMatches { get { return MatchedCells.Count > 0; } }

        public PuzzleQuestMatchResult(
            HashSet<Vector2Int> matchedCells,
            List<PuzzleQuestMatchSummary> summaries)
        {
            MatchedCells = matchedCells;
            Summaries = summaries;
        }
    }

    public struct PuzzleQuestBoardMove
    {
        public Vector2Int From;
        public Vector2Int To;
        public PuzzleGemType Type;
        public bool IsNew;

        public PuzzleQuestBoardMove(
            Vector2Int from,
            Vector2Int to,
            PuzzleGemType type,
            bool isNew)
        {
            From = from;
            To = to;
            Type = type;
            IsNew = isNew;
        }
    }

    public sealed class PuzzleQuestBoardModel
    {
        private const int GemTypeCount = 4;
        private const int MaximumGenerationAttempts = 256;

        private readonly PuzzleGemType[,] cells;
        private readonly System.Random random;

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int MinimumMatch { get; private set; }

        public PuzzleGemType this[int column, int row]
        {
            get { return cells[column, row]; }
        }

        public PuzzleQuestBoardModel(int columns, int rows, int minimumMatch, int seed)
        {
            Columns = Mathf.Clamp(columns, 5, 10);
            Rows = Mathf.Clamp(rows, 5, 10);
            MinimumMatch = 3;
            cells = new PuzzleGemType[Columns, Rows];
            random = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public void CreatePlayableBoard()
        {
            for (int attempt = 0; attempt < MaximumGenerationAttempts; attempt++)
            {
                FillWithoutImmediateMatches();
                if (HasPossibleSwap())
                {
                    return;
                }
            }

            // This deterministic pattern is only a last-resort guard. It guarantees
            // at least one legal opening move even with an unlucky random sequence.
            FillWithoutImmediateMatches();
            cells[0, 0] = PuzzleGemType.Sword;
            cells[1, 0] = PuzzleGemType.Water;
            cells[2, 0] = PuzzleGemType.Sword;
            cells[1, 1] = PuzzleGemType.Sword;
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;
        }

        public bool AreAdjacent(Vector2Int first, Vector2Int second)
        {
            return IsInside(first)
                   && IsInside(second)
                   && Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
        }

        public void Swap(Vector2Int first, Vector2Int second)
        {
            PuzzleGemType value = cells[first.x, first.y];
            cells[first.x, first.y] = cells[second.x, second.y];
            cells[second.x, second.y] = value;
        }

        public PuzzleQuestMatchResult FindMatches()
        {
            HashSet<Vector2Int> matched = new HashSet<Vector2Int>();
            Dictionary<PuzzleGemType, int> longestRuns = new Dictionary<PuzzleGemType, int>();

            for (int row = 0; row < Rows; row++)
            {
                int runStart = 0;
                for (int column = 1; column <= Columns; column++)
                {
                    bool runEnded = column == Columns
                                    || cells[column, row] != cells[runStart, row];
                    if (!runEnded)
                    {
                        continue;
                    }

                    int length = column - runStart;
                    if (length >= MinimumMatch)
                    {
                        PuzzleGemType type = cells[runStart, row];
                        RecordLongestRun(longestRuns, type, length);
                        for (int x = runStart; x < column; x++)
                        {
                            matched.Add(new Vector2Int(x, row));
                        }
                    }

                    runStart = column;
                }
            }

            for (int column = 0; column < Columns; column++)
            {
                int runStart = 0;
                for (int row = 1; row <= Rows; row++)
                {
                    bool runEnded = row == Rows
                                    || cells[column, row] != cells[column, runStart];
                    if (!runEnded)
                    {
                        continue;
                    }

                    int length = row - runStart;
                    if (length >= MinimumMatch)
                    {
                        PuzzleGemType type = cells[column, runStart];
                        RecordLongestRun(longestRuns, type, length);
                        for (int y = runStart; y < row; y++)
                        {
                            matched.Add(new Vector2Int(column, y));
                        }
                    }

                    runStart = row;
                }
            }

            if (matched.Count == 0)
            {
                return PuzzleQuestMatchResult.Empty;
            }

            Dictionary<PuzzleGemType, HashSet<Vector2Int>> cellsByType =
                new Dictionary<PuzzleGemType, HashSet<Vector2Int>>();

            foreach (Vector2Int position in matched)
            {
                PuzzleGemType type = cells[position.x, position.y];
                HashSet<Vector2Int> typeCells;
                if (!cellsByType.TryGetValue(type, out typeCells))
                {
                    typeCells = new HashSet<Vector2Int>();
                    cellsByType.Add(type, typeCells);
                }

                typeCells.Add(position);
            }

            List<PuzzleQuestMatchSummary> summaries = new List<PuzzleQuestMatchSummary>();
            for (int typeIndex = 0; typeIndex < GemTypeCount; typeIndex++)
            {
                PuzzleGemType type = (PuzzleGemType)typeIndex;
                HashSet<Vector2Int> typeCells;
                if (!cellsByType.TryGetValue(type, out typeCells))
                {
                    continue;
                }

                summaries.Add(new PuzzleQuestMatchSummary(
                    type,
                    typeCells.Count,
                    longestRuns[type],
                    typeCells));
            }

            return new PuzzleQuestMatchResult(matched, summaries);
        }

        public List<PuzzleQuestBoardMove> CollapseAndRefill(HashSet<Vector2Int> matchedCells)
        {
            List<PuzzleQuestBoardMove> moves = new List<PuzzleQuestBoardMove>();

            for (int column = 0; column < Columns; column++)
            {
                int destinationRow = 0;
                for (int sourceRow = 0; sourceRow < Rows; sourceRow++)
                {
                    Vector2Int source = new Vector2Int(column, sourceRow);
                    if (matchedCells.Contains(source))
                    {
                        continue;
                    }

                    PuzzleGemType type = cells[column, sourceRow];
                    cells[column, destinationRow] = type;
                    moves.Add(new PuzzleQuestBoardMove(
                        source,
                        new Vector2Int(column, destinationRow),
                        type,
                        false));
                    destinationRow++;
                }

                int spawnIndex = 0;
                while (destinationRow < Rows)
                {
                    PuzzleGemType type = NextGem();
                    cells[column, destinationRow] = type;
                    moves.Add(new PuzzleQuestBoardMove(
                        new Vector2Int(column, Rows + spawnIndex),
                        new Vector2Int(column, destinationRow),
                        type,
                        true));
                    destinationRow++;
                    spawnIndex++;
                }
            }

            return moves;
        }

        public bool HasPossibleSwap()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    Vector2Int current = new Vector2Int(column, row);
                    Vector2Int right = new Vector2Int(column + 1, row);
                    if (right.x < Columns && SwapCreatesMatch(current, right))
                    {
                        return true;
                    }

                    Vector2Int up = new Vector2Int(column, row + 1);
                    if (up.y < Rows && SwapCreatesMatch(current, up))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void Reshuffle()
        {
            CreatePlayableBoard();
        }

        private void FillWithoutImmediateMatches()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    List<PuzzleGemType> candidates = new List<PuzzleGemType>(GemTypeCount);
                    for (int typeIndex = 0; typeIndex < GemTypeCount; typeIndex++)
                    {
                        PuzzleGemType candidate = (PuzzleGemType)typeIndex;
                        if (!WouldCreateInitialRun(column, row, candidate))
                        {
                            candidates.Add(candidate);
                        }
                    }

                    cells[column, row] = candidates[random.Next(candidates.Count)];
                }
            }
        }

        private bool WouldCreateInitialRun(int column, int row, PuzzleGemType candidate)
        {
            if (column >= MinimumMatch - 1)
            {
                bool horizontalMatch = true;
                for (int offset = 1; offset < MinimumMatch; offset++)
                {
                    if (cells[column - offset, row] != candidate)
                    {
                        horizontalMatch = false;
                        break;
                    }
                }

                if (horizontalMatch)
                {
                    return true;
                }
            }

            if (row >= MinimumMatch - 1)
            {
                bool verticalMatch = true;
                for (int offset = 1; offset < MinimumMatch; offset++)
                {
                    if (cells[column, row - offset] != candidate)
                    {
                        verticalMatch = false;
                        break;
                    }
                }

                if (verticalMatch)
                {
                    return true;
                }
            }

            return false;
        }

        private bool SwapCreatesMatch(Vector2Int first, Vector2Int second)
        {
            Swap(first, second);
            bool createsMatch = HasMatchAt(first) || HasMatchAt(second);
            Swap(first, second);
            return createsMatch;
        }

        private bool HasMatchAt(Vector2Int position)
        {
            PuzzleGemType type = cells[position.x, position.y];
            int horizontal = 1
                             + CountDirection(position, Vector2Int.left, type)
                             + CountDirection(position, Vector2Int.right, type);
            if (horizontal >= MinimumMatch)
            {
                return true;
            }

            int vertical = 1
                           + CountDirection(position, Vector2Int.down, type)
                           + CountDirection(position, Vector2Int.up, type);
            return vertical >= MinimumMatch;
        }

        private int CountDirection(Vector2Int start, Vector2Int direction, PuzzleGemType type)
        {
            int count = 0;
            Vector2Int current = start + direction;
            while (IsInside(current) && cells[current.x, current.y] == type)
            {
                count++;
                current += direction;
            }

            return count;
        }

        private PuzzleGemType NextGem()
        {
            return (PuzzleGemType)random.Next(GemTypeCount);
        }

        private static void RecordLongestRun(
            Dictionary<PuzzleGemType, int> longestRuns,
            PuzzleGemType type,
            int length)
        {
            int current;
            if (!longestRuns.TryGetValue(type, out current) || length > current)
            {
                longestRuns[type] = length;
            }
        }
    }
}
