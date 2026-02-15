using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Logging;
using ARNavExperiment.Mission;

namespace ARNavExperiment.BeamPro
{
    public class InfoCardManager : MonoBehaviour
    {
        [SerializeField] private GameObject cardPanel;
        [SerializeField] private TextMeshProUGUI cardTitleText;
        [SerializeField] private TextMeshProUGUI cardContentText;
        [SerializeField] private Image cardImage;
        [SerializeField] private Button closeButton;

        [SerializeField] private ComparisonCardUI comparisonCard;

        private InfoCardData activeCard;
        private float cardOpenTime;
        private List<InfoCardData> availableCards = new List<InfoCardData>();

        private void Start()
        {
            closeButton?.onClick.AddListener(CloseCard);
            if (cardPanel) cardPanel.SetActive(false);
        }

        public void LoadCards(InfoCardData[] cards)
        {
            availableCards.Clear();
            availableCards.AddRange(cards);
        }

        public void TryAutoShow(string waypointId)
        {
            foreach (var card in availableCards)
            {
                if (card.autoShow && card.triggerWaypointId == waypointId)
                {
                    ShowCard(card, true);
                    break;
                }
            }
        }

        public void ShowCard(InfoCardData card, bool autoShown)
        {
            activeCard = card;
            cardOpenTime = Time.time;

            if (card.cardType == "comparison")
            {
                if (comparisonCard) comparisonCard.gameObject.SetActive(true);
                if (cardPanel) cardPanel.SetActive(false);
            }
            else
            {
                if (cardTitleText) cardTitleText.text = card.title;
                if (cardContentText) cardContentText.text = card.content;
                if (cardImage)
                {
                    cardImage.gameObject.SetActive(card.image != null);
                    if (card.image) cardImage.sprite = card.image;
                }
                if (cardPanel) cardPanel.SetActive(true);
                if (comparisonCard) comparisonCard.gameObject.SetActive(false);
            }

            EventLogger.Instance?.LogEvent("BEAM_INFO_CARD_OPENED",
                beamContentType: "info_card",
                extraData: $"{{\"card_id\":\"{card.cardId}\",\"card_type\":\"{card.cardType}\",\"auto_shown\":{autoShown.ToString().ToLower()}}}");
        }

        public void CloseCard()
        {
            if (activeCard == null) return;

            float duration = Time.time - cardOpenTime;

            EventLogger.Instance?.LogEvent("BEAM_INFO_CARD_CLOSED",
                beamContentType: "info_card",
                extraData: $"{{\"card_id\":\"{activeCard.cardId}\",\"view_duration_s\":{duration:F1}}}");

            if (cardPanel) cardPanel.SetActive(false);
            if (comparisonCard) comparisonCard.gameObject.SetActive(false);
            activeCard = null;
        }
    }
}
