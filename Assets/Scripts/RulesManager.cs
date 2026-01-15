using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RuleManager : MonoBehaviour
{
    public static RuleManager Instance { get; private set; }
    
    // Configuration parameters (set in Inspector)
    [SerializeField] private float targetMultiplier = 0.8f;
    [SerializeField] private int duplicatePenaltyAmount = 1; 
    [SerializeField] private int precisionBonusAmount = 2;   
    
    [SerializeField] private List<GameRuleSO> activeRules;

    private void Awake()
    {
        Instance = this;
    }

    // Simple method - just give it submissions and active rules
    public RoundResult ProcessSubmissions(Dictionary<int, int> submissions)
    {
        RoundResult result = new RoundResult();
        
        // Store penalty amounts in result
        result.duplicatePenaltyAmount = duplicatePenaltyAmount;
        result.precisionBonusAmount = precisionBonusAmount;
        
        // Step 1: Apply all active rules
        if (activeRules != null)
        {
            foreach (var rule in activeRules)
            {
                ApplyRule(rule, submissions, result);
            }
        }
        
        // Step 2: Calculate target (default rule always applies)
        CalculateTarget(submissions, result);
        
        // Step 3: Determine winner and losers
        DetermineWinnersAndLosers(submissions, result);
        
        return result;
    }
    
    // Even simpler: Just give submissions, get losing players
    public List<int> GetLosingPlayers(Dictionary<int, int> submissions)
    {
        RoundResult result = ProcessSubmissions(submissions);
        return result.GetAllLosingPlayerIds();
    }
    
    private void ApplyRule(GameRuleSO rule, Dictionary<int, int> submissions, RoundResult result)
    {
        switch (rule.ruleType)
        {
            case RuleType.TargetModifier:
                // Already handled in CalculateTarget
                break;
                
            case RuleType.ChoicePenalty:
                ApplyDuplicatePenalty(submissions, result);
                break;
                
            case RuleType.SpecialEffect:
                // Precision bonus handled in winner calculation
                break;
                
            case RuleType.WinCondition:
                ApplyFinalDuelRule(submissions, result);
                break;
        }
    }
    
    private void CalculateTarget(Dictionary<int, int> submissions, RoundResult result)
    {
        if (submissions.Count == 0) return;
        
        float sum = 0f;
        foreach (int value in submissions.Values)
        {
            sum += value;
        }
        
        float average = sum / submissions.Count;
        result.calculatedTarget = average * targetMultiplier;
    }
    
    private void DetermineWinnersAndLosers(Dictionary<int, int> submissions, RoundResult result)
    {
        if (submissions.Count == 0) return;
        
        // If final duel rule already determined winner, skip normal logic
        if (result.finalDuelTriggered) return;
        
        // Exclude players with duplicate penalty from winning
        var eligiblePlayers = submissions
            .Where(p => !result.penalizedPlayers.Contains(p.Key))
            .ToList();
        
        if (eligiblePlayers.Count > 0)
        {
            // Find closest to target
            var closest = eligiblePlayers
                .OrderBy(p => Mathf.Abs(p.Value - result.calculatedTarget))
                .First();
            
            result.winnerPlayerId = closest.Key;
            result.winningNumber = closest.Value;
            
            // Check for exact match (precision)
            if (Mathf.Approximately(closest.Value, result.calculatedTarget))
            {
                result.precisionHit = true;
            }
        }
        else
        {
            // All players have duplicate penalty, no winner
            result.winnerPlayerId = -1;
        }
        
        // Determine losers
        DetermineLosers(submissions, result);
    }
    
    private void DetermineLosers(Dictionary<int, int> submissions, RoundResult result)
    {
        // Default: all non-winners lose points
        foreach (var player in submissions)
        {
            if (player.Key != result.winnerPlayerId)
            {
                result.losingPlayerIds.Add(player.Key);
            }
        }
        
        // If precision hit, remove winner from losers list (they're already not included)
        // And ensure all other players are marked as losers
        if (result.precisionHit)
        {
            result.losingPlayerIds.Clear();
            foreach (var player in submissions)
            {
                if (player.Key != result.winnerPlayerId)
                {
                    result.losingPlayerIds.Add(player.Key);
                }
            }
        }
    }
    
    private void ApplyDuplicatePenalty(Dictionary<int, int> submissions, RoundResult result)
    {
        // Group by number value
        var groups = submissions
            .GroupBy(p => p.Value)
            .Where(g => g.Count() > 1);
        
        foreach (var group in groups)
        {
            foreach (var player in group)
            {
                if (!result.penalizedPlayers.Contains(player.Key))
                {
                    result.penalizedPlayers.Add(player.Key);
                }
            }
        }
    }
    
    private void ApplyFinalDuelRule(Dictionary<int, int> submissions, RoundResult result)
    {
        if (submissions.Count != 2) return;
        
        var players = submissions.Keys.ToArray();
        int player1 = players[0];
        int player2 = players[1];
        
        int p1Choice = submissions[player1];
        int p2Choice = submissions[player2];
        
        // Check for 0 vs 100
        if ((p1Choice == 0 && p2Choice == 100) || (p1Choice == 100 && p2Choice == 0))
        {
            result.finalDuelTriggered = true;
            
            if (p1Choice == 100)
            {
                result.winnerPlayerId = player1;
                result.losingPlayerIds = new List<int> { player2 };
            }
            else
            {
                result.winnerPlayerId = player2;
                result.losingPlayerIds = new List<int> { player1 };
            }
        }
    }
}

// Simple RoundResult class
public class RoundResult
{
    public int winnerPlayerId = -1;
    public int winningNumber;
    public float calculatedTarget;
    public List<int> losingPlayerIds = new List<int>();
    public List<int> penalizedPlayers = new List<int>();
    public bool precisionHit = false;
    public bool finalDuelTriggered = false;
    
    // Store penalty amounts
    public int duplicatePenaltyAmount = 1;  // Default values
    public int precisionBonusAmount = 2;    // Default values
    
    // Get all players who lose points (including penalized players)
    public List<int> GetAllLosingPlayerIds()
    {
        var allLosers = new List<int>();
        allLosers.AddRange(losingPlayerIds);
        allLosers.AddRange(penalizedPlayers);
        return allLosers.Distinct().ToList();
    }
    
    // Get point changes for each player
    public Dictionary<int, int> GetPointChanges()
    {
        var changes = new Dictionary<int, int>();
        
        // Winner gets 0 change
        if (winnerPlayerId != -1)
        {
            changes[winnerPlayerId] = 0;
        }
        
        // Losing players lose points
        int baseLoss = precisionHit ? precisionBonusAmount : 1;  // Use stored amount
        foreach (int playerId in losingPlayerIds)
        {
            changes[playerId] = -baseLoss;
        }
        
        // Penalized players lose additional point
        foreach (int playerId in penalizedPlayers)
        {
            if (changes.ContainsKey(playerId))
            {
                changes[playerId] -= duplicatePenaltyAmount;  // Use stored amount
            }
            else
            {
                changes[playerId] = -duplicatePenaltyAmount;  // Use stored amount
            }
        }
        
        return changes;
    }
}