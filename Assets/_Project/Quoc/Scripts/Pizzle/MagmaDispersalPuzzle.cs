using System;
using System.Collections.Generic;

/// <summary>
/// State model for three vessels connected through a shared reservoir.
/// When the reservoir is empty, a valve draws its vessel into it; otherwise it fills that vessel.
/// </summary>
public sealed class MagmaDispersalPuzzle
{
    public const int VesselCount = 3;
    public const int CoreVessel = 0;
    public const int TargetAmount = 10;
    public const int SharedReservoir = VesselCount;

    private static readonly int[] Capacities = { 13, 8, 5 };
    private static readonly int[] StartingAmounts = { 13, 0, 0 };
    private static readonly PuzzleAction[] SearchOrder = { PuzzleAction.SwapLeft, PuzzleAction.Transfer, PuzzleAction.SwapRight };

    private readonly int[] amounts = new int[VesselCount];
    private int sharedAmount;

    public int MoveCount { get; private set; }
    public bool IsSolved => amounts[CoreVessel] == TargetAmount;
    public int TotalMagma => amounts[0] + amounts[1] + amounts[2] + sharedAmount;

    public MagmaDispersalPuzzle() => Reset();
    public int GetCapacity(int vesselIndex) => Capacities[ClampVessel(vesselIndex)];
    public int GetAmount(int vesselIndex) => amounts[ClampVessel(vesselIndex)];
    public int GetSharedAmount() => sharedAmount;
    public int GetSharedCapacity() => Capacities[CoreVessel];
    public int GetVesselAtSlot(int slotIndex) => ClampVessel(slotIndex);
    public int GetSlotOfVessel(int vesselIndex) => ClampVessel(vesselIndex);

    public MoveResult Apply(PuzzleAction action)
    {
        if (IsSolved) return MoveResult.Blocked(action);

        int vessel = GetVesselForAction(action);
        bool drawsIntoShared = sharedAmount == 0;
        int transferredAmount = drawsIntoShared
            ? Math.Min(amounts[vessel], GetSharedCapacity() - sharedAmount)
            : Math.Min(sharedAmount, Capacities[vessel] - amounts[vessel]);
        bool changed = transferredAmount > 0;
        if (changed)
        {
            if (drawsIntoShared)
            {
                amounts[vessel] -= transferredAmount;
                sharedAmount += transferredAmount;
            }
            else
            {
                sharedAmount -= transferredAmount;
                amounts[vessel] += transferredAmount;
            }

            MoveCount++;
        }

        return new MoveResult(action, changed, vessel, SharedReservoir, transferredAmount, drawsIntoShared, IsSolved);
    }

    public void Reset()
    {
        Array.Copy(StartingAmounts, amounts, VesselCount);
        sharedAmount = 0;
        MoveCount = 0;
    }

    public PuzzleAction? FindHint()
    {
        StateKey start = new StateKey(amounts[0], amounts[1], amounts[2], sharedAmount);
        if (IsGoal(start)) return null;

        Queue<SearchNode> frontier = new Queue<SearchNode>();
        HashSet<StateKey> visited = new HashSet<StateKey> { start };
        frontier.Enqueue(new SearchNode(start, null));
        while (frontier.Count > 0)
        {
            SearchNode node = frontier.Dequeue();
            foreach (PuzzleAction action in SearchOrder)
            {
                StateKey next = ApplyToState(node.State, action, out bool changed);
                if (!changed || !visited.Add(next)) continue;
                PuzzleAction firstAction = node.FirstAction ?? action;
                if (IsGoal(next)) return firstAction;
                frontier.Enqueue(new SearchNode(next, firstAction));
            }
        }

        return null;
    }

    private static int GetVesselForAction(PuzzleAction action)
    {
        switch (action)
        {
            case PuzzleAction.SwapLeft: return 0;
            case PuzzleAction.Transfer: return 1;
            default: return 2;
        }
    }

    private static StateKey ApplyToState(StateKey state, PuzzleAction action, out bool changed)
    {
        int[] stateAmounts = { state.Amount0, state.Amount1, state.Amount2 };
        int vessel = GetVesselForAction(action);
        bool drawsIntoShared = state.SharedAmount == 0;
        int transfer = drawsIntoShared
            ? Math.Min(stateAmounts[vessel], Capacities[CoreVessel] - state.SharedAmount)
            : Math.Min(state.SharedAmount, Capacities[vessel] - stateAmounts[vessel]);
        changed = transfer > 0;
        if (changed)
        {
            if (drawsIntoShared)
            {
                stateAmounts[vessel] -= transfer;
                return new StateKey(stateAmounts[0], stateAmounts[1], stateAmounts[2], state.SharedAmount + transfer);
            }

            stateAmounts[vessel] += transfer;
            return new StateKey(stateAmounts[0], stateAmounts[1], stateAmounts[2], state.SharedAmount - transfer);
        }

        return state;
    }

    private static bool IsGoal(StateKey state) => state.Amount0 == TargetAmount;
    private static int ClampVessel(int vesselIndex) => Math.Max(0, Math.Min(VesselCount - 1, vesselIndex));

    public enum PuzzleAction { SwapLeft, Transfer, SwapRight }

    public readonly struct MoveResult
    {
        public PuzzleAction Action { get; }
        public bool Changed { get; }
        public int SourceVessel { get; }
        public int DestinationVessel { get; }
        public int TransferredAmount { get; }
        public bool DrawsIntoShared { get; }
        public bool Solved { get; }

        public MoveResult(PuzzleAction action, bool changed, int vessel, int sharedReservoir, int transferredAmount, bool drawsIntoShared, bool solved)
        {
            Action = action;
            Changed = changed;
            SourceVessel = drawsIntoShared ? vessel : sharedReservoir;
            DestinationVessel = drawsIntoShared ? sharedReservoir : vessel;
            TransferredAmount = transferredAmount;
            DrawsIntoShared = drawsIntoShared;
            Solved = solved;
        }

        public static MoveResult Blocked(PuzzleAction action) => new MoveResult(action, false, 0, SharedReservoir, 0, false, true);
    }

    private readonly struct SearchNode
    {
        public StateKey State { get; }
        public PuzzleAction? FirstAction { get; }
        public SearchNode(StateKey state, PuzzleAction? firstAction) { State = state; FirstAction = firstAction; }
    }

    private readonly struct StateKey : IEquatable<StateKey>
    {
        public int Amount0 { get; }
        public int Amount1 { get; }
        public int Amount2 { get; }
        public int SharedAmount { get; }
        public StateKey(int amount0, int amount1, int amount2, int sharedAmount) { Amount0 = amount0; Amount1 = amount1; Amount2 = amount2; SharedAmount = sharedAmount; }
        public bool Equals(StateKey other) => Amount0 == other.Amount0 && Amount1 == other.Amount1 && Amount2 == other.Amount2 && SharedAmount == other.SharedAmount;
        public override bool Equals(object obj) => obj is StateKey other && Equals(other);
        public override int GetHashCode() { unchecked { return (((Amount0 * 397) ^ Amount1) * 397 ^ Amount2) * 397 ^ SharedAmount; } }
    }
}
