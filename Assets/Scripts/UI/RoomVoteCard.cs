using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Core;
using Category5.Map;

namespace Category5.UI
{
    // individual room card in the vote UI
    // shows room info and a vote button
    public class RoomVoteCard : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI voteCountText;
        [SerializeField] private Button voteButton;

        private int _roomIndex;
        private StormRoomData _roomData;

        public void Setup(int roomIndex, StormRoomData roomData)
        {
            _roomIndex = roomIndex;
            _roomData = roomData;

            if (roomNameText != null)
            {
                roomNameText.text = $"Room {roomIndex}";
            }

            if (difficultyText != null)
            {
                string ringLabel = roomData.eyewallIndex == -1 ? "Eye (Boss)" : $"Ring {roomData.eyewallIndex}";
                difficultyText.text = ringLabel;
            }

            if (voteCountText != null)
            {
                voteCountText.text = "0 votes";
            }

            if (voteButton != null)
            {
                voteButton.onClick.AddListener(OnVoteClicked);
            }
        }

        private void OnVoteClicked()
        {
            if (RoomVoteManager.Instance != null)
            {
                RoomVoteManager.Instance.CastVoteServerRpc(_roomIndex);
            }
        }

        private void OnDestroy()
        {
            if (voteButton != null)
            {
                voteButton.onClick.RemoveListener(OnVoteClicked);
            }
        }
    }
}
