using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Random = UnityEngine.Random;

public class NetworkGameManager : NetworkBehaviour
{
   public static NetworkGameManager Instance {get; private set; }

   [SerializeField] private PanelManager _panelManager;
   public PanelManager PanelManager => _panelManager;
   
   [Range(1,10)]
   [SerializeField] private int countDownSeconds = 3;

   private NetworkVariable<int> currentRound = new NetworkVariable<int>(1);

   private List<NetworkPlayer> players = new List<NetworkPlayer>();
   
   public NetworkVariable<int> timer = new NetworkVariable<int>(25);

   public event Action OnTimerEnd;

   [SerializeField] private int DefaultWaitTime = 25;
   private Coroutine countDownCoroutine;
   public void StartCountDown()
   {
      if(winnerFound) return;
      if (countDownCoroutine != null)
      {
         StopCoroutine(countDownCoroutine);
         countDownCoroutine = null;
      }
      EnableTimerTextClientRPC();
      countDownCoroutine = StartCoroutine(CountDownCoroutine());
   }

   private IEnumerator CountDownCoroutine()
   {
      timer.Value = DefaultWaitTime;
      while (timer.Value > 0)
      {
         yield return new WaitForSeconds(1f);
         timer.Value--;
      }
      FireTimerCountdownEndClientRPC();
   }
   [ClientRpc]
   private void FireTimerCountdownEndClientRPC()
   {
      Debug.Log($"Countdown finished in {countDownSeconds} Secs");
      OnTimerEnd?.Invoke();
   }

   [ClientRpc]
   private void EnableTimerTextClientRPC()
   {
      _panelManager.EnableTimerText();
   }

   public void RegisterTimerValueChanged(NetworkVariable<int>.OnValueChangedDelegate onValueChanged)
   {
      timer.OnValueChanged -= onValueChanged;
      timer.OnValueChanged += onValueChanged;
   }

   public void RegisterOnCurrentRoundValueChanged(NetworkVariable<int>.OnValueChangedDelegate onValueChanged)
   {
      currentRound.OnValueChanged -= onValueChanged;
      currentRound.OnValueChanged += onValueChanged;
   }

   public void RegisterMeToTheMatch(NetworkPlayer player)
   {
      if (!players.Contains(player))
      {
         players.Add(player);
      }
   }
   
   Dictionary<int,int> submittedNumbersDict = new Dictionary<int, int>();

   public void SubmittedNumber(int playerId, int currentNumber)
   {
      if(currentNumber < 0) // Means Resetting
         return;
      //Check if All Player Submitted

      if (!submittedNumbersDict.ContainsKey(playerId))
      {
         submittedNumbersDict.Add(playerId, currentNumber);
      }
      
      var activePlayers = GetActivePlayers();

      if (IsServer && activePlayers.Count > 0 && activePlayers.Count == submittedNumbersDict.Count)
      {
         // Need to Start the Timer
         
         Debug.Log($"Round Finishing in {countDownSeconds} Secs");

         if (countDownCoroutine != null)
         {
            StopCoroutine(countDownCoroutine);
            countDownCoroutine = null;
         }
         FireTimerCountdownEndClientRPC();
         StartCountDownClientRPC(countDownSeconds);
         
         Invoke(nameof(AdvanceRound),countDownSeconds);
      }
   }

   private Dictionary<int, int> GetPlayerScores()
   {
      Dictionary<int, int> playerPoints = new Dictionary<int, int>();
      List<NetworkPlayer> activePlayers = GetActivePlayers();
      foreach (NetworkPlayer player in activePlayers)
      {
         if (!playerPoints.ContainsKey(player.GetPlayerID()))
         {
            playerPoints.Add(player.GetPlayerID(), player.GetCurrentScore());
         }
      }
      return playerPoints;
   }
   [ClientRpc]
   private void UpdateAfterRoundUIClientRPC(int winnerId, float targetNumber)
   {
      if (_panelManager != null)
      {
         _panelManager.UpdateAfterRoundUI(winnerId, targetNumber);
      }
   }
   private void AdvanceRound()
   {
      if (IsServer && !winnerFound)
      {
         int temp = currentRound.Value;
         temp+= 1;
         currentRound.Value = temp;
         
         Dictionary<int, int> playerScores = GetPlayerScores();
         
         // Get Round Results
         RoundResult result = RuleManager.Instance.ProcessSubmissions(submittedNumbersDict);
         if (result != null)
         {
            Debug.Log($"Target: {result.calculatedTarget:F1}");
            Debug.Log($"Winner: Player {result.winnerPlayerId}");
            
            UpdateAfterRoundUIClientRPC(result.winnerPlayerId,result.calculatedTarget);
        
            // Apply point changes
            Dictionary<int, int> pointChanges = result.GetPointChanges();
            Debug.Log("\nPoint Changes:");
            foreach (var change in pointChanges)
            {
               playerScores[change.Key] += change.Value;
               string sign = change.Value >= 0 ? "+" : "";
               Debug.Log($"Player {change.Key}: {sign}{change.Value} points (Total: {playerScores[change.Key]})");
            }
            
            UpdatePointChanges(playerScores);
            StartCountDown();
         }
      }
      submittedNumbersDict.Clear();
   }

   private bool winnerFound = false;
   private void UpdatePointChanges(Dictionary<int, int> pointChanges)
   {
      foreach (var change in pointChanges)
      {
         NetworkPlayer player = players.Where(p => p.GetPlayerID() == change.Key).FirstOrDefault();
         if (player != null)
         {
            player.UpdateCurrentScore(change.Value);
         }
      }
      
      // Check for Winner - When only two players remain
      if (pointChanges.Count == 2)
      {
         List<int> remainingPlayers = new List<int>();
    
         // Find which players still have points
         foreach (var player in pointChanges)
         {
            if (player.Value > 0) // Player still has points
            {
               remainingPlayers.Add(player.Key);
            }
         }
    
         // If only one player has points > 0, they win
         if (remainingPlayers.Count == 1)
         {
            int winnerId = remainingPlayers[0];
            Debug.Log($"<color=yellow> Winner Found: Player_{winnerId} </color>");
            winnerFound = true;
            AnnounceWinnerClientRPC(winnerId);
            return;
         }
         // If both still have points, game continues
         else if (remainingPlayers.Count == 2)
         {
            Debug.Log("Both players still in the game - continuing...");
            // Continue to next round
         }
         // This shouldn't happen, but handle just in case
         else if (remainingPlayers.Count == 0)
         {
            Debug.LogError("No players remaining - unexpected state!");
            winnerFound = true;
            //EndGame(-1); // Draw or error
         }
      }
   }

   private List<NetworkPlayer> GetActivePlayers()
   {
      List<NetworkPlayer> activePlayers = new List<NetworkPlayer>();
      foreach (NetworkPlayer player in players)
      {
         if (player.GetCurrentPlayerState() == PlayerState.Active)
         {
            activePlayers.Add(player);
         }
      }
      return activePlayers;
   }
   
   

   private void Awake()
   {
      Instance = this;
   }


   #region RPC Methods
   
   [ClientRpc]
   private void StartCountDownClientRPC(int countDownSeconds)
   {
      if (_panelManager != null)
      {
         _panelManager.StartCountdown(countDownSeconds);
      }
   }
   [ClientRpc]
   private void AnnounceWinnerClientRPC(int winnerId)
   {
      if (_panelManager != null)
      {
         _panelManager.AnnounceWinner(winnerId);
      }
   }

   #endregion
   
   #if UNITY_EDITOR

   [Range(1,10)]
   [SerializeField] private int roundDebug = 1;
   
   [ContextMenu("DebugCurrentRound")]
   private void DebugCurrentRound()
   {
      if (IsServer)
      {
         currentRound.Value = roundDebug;
      }
   }
   
   #endif
}
