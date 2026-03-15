using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Presentation.BeamPro
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

        private void OnEnable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(Language lang)
        {
            // Refresh visible card text if a card is open
            if (activeCard != null && cardPanel != null && cardPanel.activeSelf)
            {
                if (cardTitleText) cardTitleText.text = activeCard.GetTitle();
                if (cardContentText) cardContentText.text = activeCard.GetContent();
            }
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
                if (cardTitleText) cardTitleText.text = card.GetTitle();
                if (cardContentText) cardContentText.text = card.GetContent();
                if (cardImage)
                {
                    cardImage.gameObject.SetActive(card.image != null);
                    if (card.image) cardImage.sprite = card.image;
                }
                if (cardPanel) cardPanel.SetActive(true);
                if (comparisonCard) comparisonCard.gameObject.SetActive(false);
            }

            DomainEventBus.Instance?.Publish(new BeamInfoCardToggled(card.cardId, true));
        }

        public void CloseCard()
        {
            if (activeCard == null) return;

            float viewDuration = Time.time - cardOpenTime;
            DomainEventBus.Instance?.Publish(new BeamInfoCardToggled(activeCard.cardId, false, viewDuration));

            if (cardPanel) cardPanel.SetActive(false);
            if (comparisonCard) comparisonCard.gameObject.SetActive(false);
            activeCard = null;
        }
    }
}
