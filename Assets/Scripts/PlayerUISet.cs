using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUISet : MonoBehaviour
{
    private NetworkPlayer _networkPlayer;

    [SerializeField] private TextMeshProUGUI playerNameText;

    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TMP_InputField numberInput;
    [SerializeField] private Button submitButton;
    
    [SerializeField] private GameObject elimitatedObject;

    private void Awake()
    {
        numberInput.onValueChanged.AddListener(value =>
        {
            if (int.TryParse(value, out int number))
            {
                if (number < 0 || number > 100)
                {
                    numberInput.SetTextWithoutNotify(string.Empty);
                }
            }
        });
        elimitatedObject.SetActive(false);
    }

    public void SetPlayer(NetworkPlayer networkPlayer)
    {
      _networkPlayer = networkPlayer;
      if (_networkPlayer != null)
      {
          string temp = "Player_" + _networkPlayer.GetPlayerID().ToString();
          name = temp;
          if(playerNameText != null)
            playerNameText.SetText(temp);
          
          if (!_networkPlayer.IsOwner)
          {
              submitButton.gameObject.SetActive(false);
              numberInput.interactable = false;
              numberInput.contentType = TMP_InputField.ContentType.Password;
          }
          else
          {
              submitButton.onClick.RemoveAllListeners();
              submitButton.onClick.AddListener(() =>
              {
                  if (int.TryParse(numberInput.text, out int number))
                  {
                      SetSubmitButtonInteractable(false);
                      _networkPlayer.SetCurrentNumber(number);
                  }
              });
          }

          if (scoreText != null)
          {
              scoreText.SetText(networkPlayer.GetCurrentScore().ToString());
          }
          _networkPlayer.RegisterCurrentScoreValueChanged(OnScoreValueChanged);
          _networkPlayer.RegisterCurrentNumberValueChanged(OnCurrentNumberChanged);
          _networkPlayer.RegisterPlayerStateChanges(OnPlayerStateChanged);
      }
    }

    private void OnPlayerStateChanged(int oldState, int newState)
    {
        if ((PlayerState)newState == PlayerState.Eliminated)
        {
            // Eliminate the Player
            elimitatedObject.SetActive(true);
        }
    }

    private void OnScoreValueChanged(int oldScore, int newScore)
    {
        if (scoreText != null)
        {
            scoreText.SetText(newScore.ToString());
        }
    }

    private void OnCurrentNumberChanged(int oldNumber, int newNumber)
    {
        if (numberInput != null)
        {
            string temp = newNumber > 0 ? newNumber.ToString() : string.Empty; 
            numberInput.SetTextWithoutNotify(temp);
        }
    }
    
    public void SetSubmitButtonInteractable(bool interactable)
    {
        submitButton.interactable = interactable;
    }
    
    public int GetInputValue()
    {
        if (int.TryParse(numberInput.text, out var result))
        {
            return result;
        }
        return -1;
    }
}
