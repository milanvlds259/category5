using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Map;
using TMPro;

namespace Category5.UI
{
    // full-screen overlay UI shown inside the van during voting
    // displays connected rooms as cards with vote buttons
    // shows live vote counts and "waiting for players..." state
    public class RoomVoteUI : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private GameObject votePanel;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject roomCardPrefab;

        [Header("text")]
        [SerializeField] private TextMeshProUGUI statusText;

        private List<GameObject> _spawnedCards = new List<GameObject>();

        private void OnEnable()
        {
            RoomManager.OnVoteStarted += HandleVoteStarted;
            RoomManager.OnVoteResolved += HandleVoteResolved;
        }

        private void OnDisable()
        {
            RoomManager.OnVoteStarted -= HandleVoteStarted;
            RoomManager.OnVoteResolved -= HandleVoteResolved;
        }

        private void HandleVoteStarted()
        {
            if (votePanel != null) votePanel.SetActive(true);
            RefreshCards();
            UpdateStatus();
        }

        private void HandleVoteResolved(int winningRoom)
        {
            if (votePanel != null) votePanel.SetActive(false);
            ClearCards();
        }

        private void RefreshCards()
        {
            ClearCards();

            if (RoomManager.Instance == null) return;

            // get the current room's connected rooms
            int currentRoomIdx = RoomManager.Instance.CurrentRoomIndex.Value;
            var layout = GetLayout();
            if (layout == null) return;

            List<int> connectedRooms = layout.GetConnectedRooms(currentRoomIdx);

            foreach (int roomIdx in connectedRooms)
            {
                GameObject cardObj = Instantiate(roomCardPrefab, cardContainer);
                RoomVoteCard card = cardObj.GetComponent<RoomVoteCard>();
                if (card != null)
                {
                    card.Setup(roomIdx, layout.GetRoom(roomIdx));
                }
                _spawnedCards.Add(cardObj);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null) Destroy(card);
            }
            _spawnedCards.Clear();
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;

            int connectedCount = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ConnectedClientsIds.Count
                : 0;

            statusText.text = $"Waiting for {connectedCount} player{(connectedCount == 1 ? "" : "s")} to vote...";
        }

        private MapLayout GetLayout()
        {
            // access layout via RoomManager — we need to expose it
            // for now, use reflection or add a public getter
            // simpler: store a reference when RoomManager sets it
            return _cachedLayout;
        }

        private MapLayout _cachedLayout;

        public void SetLayout(MapLayout layout)
        {
            _cachedLayout = layout;
        }

        private void Update()
        {
            if (votePanel != null && votePanel.activeSelf)
            {
                UpdateStatus();
            }
        }
    }
}
