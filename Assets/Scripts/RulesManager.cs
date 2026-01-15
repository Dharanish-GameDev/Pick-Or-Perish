using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RuleManager : MonoBehaviour
{
    public static RuleManager Instance { get; private set; }
    
    [SerializeField] private float targetMultiplier = 0.8f;
    [SerializeField] private bool enableLogs = true;
    [SerializeField] private List<GameRuleSO> activeRules;

    private void Awake()
    {
        Instance = this;
    }

    public RoundResult ProcessSubmissions(Dictionary<int, int> submissions)
    {
        RoundResult result = new RoundResult();
        
        Log($"--- Processing Round with {submissions.Count} players ---");
        LogActiveRules();
        
        // Step 0: Check if final duel applies (2 players)
        bool isFinalDuel = submissions.Count == 2;
        Log($"Players: {submissions.Count}, Is Final Duel: {isFinalDuel}");
        
        // Step 1: Calculate target
        CalculateTarget(submissions, result);
        Log($"Calculated Target: {result.calculatedTarget:F2} (Average: {result.calculatedTarget / targetMultiplier:F2} × {targetMultiplier})");
        
        // Step 2: Apply Final Duel Rule if applicable
        if (isFinalDuel && HasRule(RuleType.WinCondition))
        {
            Log($"Checking Final Duel Rule...");
            if (ApplyFinalDuelRule(submissions, result))
            {
                Log($"<color=orange>FINAL DUEL TRIGGERED! Player {result.winnerPlayerId} (100) wins automatically!</color>");
                Log($"All other rules are ignored due to Final Duel override.");
                return result;
            }
            else
            {
                Log($"Final Duel not triggered (not 0 vs 100), continuing with normal rules.");
            }
        }
        
        // Step 3: Apply Duplicate Penalty (Rule 2)
        if (HasRule(RuleType.ChoicePenalty))
        {
            Log($"Applying Duplicate Penalty Rule...");
            ApplyDuplicatePenalty(submissions, result);
        }
        else
        {
            Log($"Duplicate Penalty Rule is not active.");
        }
        
        // Step 4: Determine winner (ignoring duplicate penalty per rule)
        Log($"Determining winner...");
        DetermineWinner(submissions, result);
        Log($"Winner: Player {result.winnerPlayerId} with number {result.winningNumber}");
        
        // Step 5: Apply Precision Rule (Rule 3) - affects loser penalty amount
        if (HasRule(RuleType.SpecialEffect))
        {
            Log($"Checking Precision Rule...");
            ApplyPrecisionRule(submissions, result);
        }
        else
        {
            Log($"Precision Rule is not active.");
        }
        
        Log($"--- Round Processing Complete ---");
        return result;
    }
    
    private bool HasRule(RuleType ruleType)
    {
        return activeRules != null && activeRules.Any(r => r.ruleType == ruleType);
    }
    
    private void LogActiveRules()
    {
        if (!enableLogs) return;
        
        Log($"Active Rules ({activeRules?.Count ?? 0}):");
        if (activeRules != null)
        {
            foreach (var rule in activeRules)
            {
                Log($"  - {rule.ruleName}: {rule.description}");
            }
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
    
    private void ApplyDuplicatePenalty(Dictionary<int, int> submissions, RoundResult result)
    {
        // Group by number value
        var groups = submissions
            .GroupBy(p => p.Value)
            .Where(g => g.Count() > 1);
        
        bool foundDuplicates = false;
        
        foreach (var group in groups)
        {
            foundDuplicates = true;
            string players = string.Join(", ", group.Select(p => $"Player {p.Key}({p.Value})"));
            Log($"<color=yellow>DUPLICATE FOUND:</color> Number {group.Key} chosen by {players}");
            
            foreach (var player in group)
            {
                if (!result.duplicatePlayers.Contains(player.Key))
                {
                    result.duplicatePlayers.Add(player.Key);
                    Log($"  Player {player.Key} marked for duplicate penalty (-1 point)");
                }
            }
        }
        
        if (!foundDuplicates)
        {
            Log($"No duplicate numbers found.");
        }
        
        Log($"Total players with duplicate penalty: {result.duplicatePlayers.Count}");
    }
    
    private void DetermineWinner(Dictionary<int, int> submissions, RoundResult result)
    {
        if (submissions.Count == 0) return;
        
        // Find closest to target, IGNORING duplicate penalty
        var allPlayers = submissions.ToList();
        
        // Log all distances for debugging
        Log($"Player distances from target {result.calculatedTarget:F2}:");
        foreach (var player in allPlayers)
        {
            float distance = Mathf.Abs(player.Value - result.calculatedTarget);
            Log($"  Player {player.Key}: {player.Value} (distance: {distance:F2})");
        }
        
        var closest = allPlayers
            .OrderBy(p => Mathf.Abs(p.Value - result.calculatedTarget))
            .First();
        
        result.winnerPlayerId = closest.Key;
        result.winningNumber = closest.Value;
        
        float winningDistance = Mathf.Abs(closest.Value - result.calculatedTarget);
        Log($"Closest: Player {closest.Key} with {closest.Value} (distance: {winningDistance:F2})");
    }
    
    private void ApplyPrecisionRule(Dictionary<int, int> submissions, RoundResult result)
    {
        // Check if winner picked exact target number
        if (Mathf.Abs(result.winningNumber - result.calculatedTarget) < 0.0001f)
        {
            result.precisionHit = true;
            Log($"<color=cyan>PRECISION HIT!</color> Player {result.winnerPlayerId} chose exact target number ({result.winningNumber})");
            Log($"All other players will lose 2 points instead of 1");
        }
        else
        {
            float difference = Mathf.Abs(result.winningNumber - result.calculatedTarget);
            Log($"No precision hit. Difference: {difference:F2}");
        }
    }
    
    private bool ApplyFinalDuelRule(Dictionary<int, int> submissions, RoundResult result)
    {
        if (submissions.Count != 2) return false;
        
        var entries = submissions.ToArray();
        int player1 = entries[0].Key;
        int choice1 = entries[0].Value;
        int player2 = entries[1].Key;
        int choice2 = entries[1].Value;
        
        Log($"Final Duel Choices: Player {player1}= {choice1}, Player {player2}= {choice2}");
        
        // Check for 0 vs 100
        if ((choice1 == 0 && choice2 == 100) || (choice1 == 100 && choice2 == 0))
        {
            result.finalDuelTriggered = true;
            
            // 100 automatically wins
            if (choice1 == 100)
            {
                result.winnerPlayerId = player1;
                result.winningNumber = 100;
            }
            else
            {
                result.winnerPlayerId = player2;
                result.winningNumber = 100;
            }
            
            // Loser is the other player
            result.losingPlayerIds.Add(player1 == result.winnerPlayerId ? player2 : player1);
            
            Log($"FINAL DUEL: 0 vs 100 detected!");
            Log($"Player {result.winnerPlayerId} (100) wins automatically!");
            
            return true; // Final duel applied
        }
        
        return false; // Not 0 vs 100, normal rules apply
    }
    
    private void Log(string message)
    {
        if (enableLogs)
        {
            Debug.Log($"[RuleManager] {message}");
        }
    }
}

// Updated RoundResult with logging in point calculation
public class RoundResult
{
    public int winnerPlayerId = -1;
    public int winningNumber;
    public float calculatedTarget;
    public List<int> losingPlayerIds = new List<int>();
    public List<int> duplicatePlayers = new List<int>();
    public bool precisionHit = false;
    public bool finalDuelTriggered = false;
    
    // Get point changes for each player
    public Dictionary<int, int> GetPointChanges()
    {
        var changes = new Dictionary<int, int>();
        
        // Initialize all players with 0
        var allPlayers = new List<int>();
        if (winnerPlayerId != -1) allPlayers.Add(winnerPlayerId);
        allPlayers.AddRange(losingPlayerIds);
        allPlayers.AddRange(duplicatePlayers);
        allPlayers = allPlayers.Distinct().ToList();
        
        foreach (int playerId in allPlayers)
        {
            changes[playerId] = 0;
        }
        
        Debug.Log($"--- Calculating Point Changes ---");
        Debug.Log($"Winner: Player {winnerPlayerId} ({winningNumber})");
        Debug.Log($"Duplicate Players: {string.Join(", ", duplicatePlayers)}");
        Debug.Log($"Precision Hit: {precisionHit}");
        Debug.Log($"Final Duel: {finalDuelTriggered}");
        
        // RULE 2: Duplicate Penalty
        Debug.Log($"Applying Duplicate Penalty:");
        foreach (int playerId in duplicatePlayers)
        {
            changes[playerId] -= 1;
            Debug.Log($"  Player {playerId}: -1 point (duplicate) = {changes[playerId]} total");
        }
        
        // RULE 3: Precision Rule
        int loserPointLoss = precisionHit ? 2 : 1;
        if (precisionHit)
        {
            Debug.Log($"Precision Rule Active: Losers lose {loserPointLoss} points each");
        }
        else
        {
            Debug.Log($"Normal Loss: Losers lose {loserPointLoss} point each");
        }
        
        // Determine all losers (all non-winners)
        if (winnerPlayerId != -1)
        {
            foreach (var playerId in allPlayers)
            {
                if (playerId != winnerPlayerId && !losingPlayerIds.Contains(playerId))
                {
                    losingPlayerIds.Add(playerId);
                }
            }
        }
        
        Debug.Log($"Losing Players: {string.Join(", ", losingPlayerIds)}");
        
        // Apply loser penalties
        Debug.Log($"Applying Loser Penalties:");
        foreach (int playerId in losingPlayerIds)
        {
            int previous = changes.ContainsKey(playerId) ? changes[playerId] : 0;
            changes[playerId] = previous - loserPointLoss;
            Debug.Log($"  Player {playerId}: -{loserPointLoss} point(s) (lose) = {changes[playerId]} total");
        }
        
        // Log final results
        Debug.Log($"--- Final Point Changes ---");
        foreach (var kvp in changes)
        {
            string reason = "";
            if (kvp.Key == winnerPlayerId)
            {
                reason = " (WINNER";
                if (duplicatePlayers.Contains(kvp.Key)) reason += " + DUPLICATE)";
                else reason += ")";
            }
            else
            {
                reason = " (LOSER";
                if (duplicatePlayers.Contains(kvp.Key)) reason += " + DUPLICATE)";
                else reason += ")";
            }
            
            Debug.Log($"Player {kvp.Key}: {kvp.Value} points{reason}");
        }
        
        return changes;
    }
    
    // Helper to get all losing player IDs (for elimination check)
    public List<int> GetAllLosingPlayerIds()
    {
        var changes = GetPointChanges();
        var losingPlayers = new List<int>();
        
        foreach (var kvp in changes)
        {
            if (kvp.Value < 0)
            {
                losingPlayers.Add(kvp.Key);
            }
        }
        
        Debug.Log($"Players losing points this round: {string.Join(", ", losingPlayers)}");
        return losingPlayers.Distinct().ToList();
    }
}