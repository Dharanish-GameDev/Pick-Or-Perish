using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PanelManager : MonoBehaviour
{
   [SerializeField] private GameObject connectionPanel;
   [SerializeField] private GameObject gameplayPanel;
   
   [SerializeField] private RectTransform playerUISetsParent;
   [SerializeField] private PlayerUISet playerUISetRef;

   [SerializeField] private TextMeshProUGUI roundCountText;
   
   [SerializeField] private GameObject timerObject;
   
   [SerializeField] private TextMeshProUGUI timeText;
   
   private Coroutine countdownRoutine;

   [SerializeField] private GameObject afterRoundObj;
   [SerializeField] private TextMeshProUGUI targetNumberText;
   [SerializeField] private TextMeshProUGUI winnerNameText;

   [SerializeField] private GameObject winnerUIObj;
   [SerializeField] private TextMeshProUGUI overallWinnerText;
   
   private void Awake()
   {
      connectionPanel.SetActive(true);
      gameplayPanel.SetActive(false);
   }

   private void Start()
   {
      NetworkCallsManager.Instance.RegisterOnConnectedToNetwork(EnableGamePlayPanel);
      NetworkGameManager.Instance.RegisterOnCurrentRoundValueChanged(OnRoundValueChanged);
   }

   public void AnnounceWinner(int winnerId)
   {
      winnerUIObj.SetActive(true);
      overallWinnerText.text = "Player_" + winnerId;
   }

   public void EnableGamePlayPanel()
   {
      connectionPanel.SetActive(false);
      gameplayPanel.SetActive(true);
   }

   public PlayerUISet CreatePlayerUISet()
   {
      PlayerUISet temp = null;
      temp = Instantiate(playerUISetRef, playerUISetsParent);
      return temp;
   }

   private void OnRoundValueChanged(int previousRound, int currentRound)
   {
      if (roundCountText != null)
      {
         roundCountText.text = currentRound.ToString();
      }
   }

   public void StartCountdown(int timeInSeconds)
   {
      if (countdownRoutine != null)
         StopCoroutine(countdownRoutine);
      
      afterRoundObj.SetActive(false);
      timerObject.SetActive(true);
      countdownRoutine = StartCoroutine(Countdown(timeInSeconds));
   }

   private IEnumerator Countdown(int time)
   {
      while (time > 0)
      {
         UpdateTimerUI(time);
         yield return new WaitForSeconds(1f);
         time--;
      }

      UpdateTimerUI(0);
      timerObject.SetActive(false); // hide when done
   }

   private void UpdateTimerUI(int time)
   {
      int minutes = time / 60;
      int seconds = time % 60;
      timeText.text = $"{minutes:00}:{seconds:00}";
   }

   public void UpdateAfterRoundUI(int playerId, float targetNumber)
   {
      afterRoundObj.SetActive(true);
      
      winnerNameText.text = "Player_" + playerId;

      targetNumberText.text = Mathf.RoundToInt(targetNumber).ToString();
   }
}

