using System.Collections;
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

        [Header("Card List")]
        [SerializeField] private GameObject cardListContainer;
        [SerializeField] private GameObject cardItemTemplate;
        [SerializeField] private TextMeshProUGUI cardListEmptyText;

        private InfoCardData activeCard;
        private float cardOpenTime;
        private List<InfoCardData> availableCards = new List<InfoCardData>();
        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        private void Start()
        {
            closeButton?.onClick.AddListener(CloseCard);
            if (cardPanel) cardPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
            ShowListView();

            // 자가 복구: 카드가 비어있으면 현재 미션에서 직접 로드 시도
            if (availableCards.Count == 0)
                TryLoadFromCurrentMission();

            if (availableCards.Count > 0)
                RebuildCardList();
            else
                ShowEmptyState();

            StartCoroutine(ForceLayoutRebuild());
        }

        private IEnumerator ForceLayoutRebuild()
        {
            yield return null; // 1프레임 대기 후 레이아웃 갱신
            if (cardListContainer)
            {
                Canvas.ForceUpdateCanvases();
                var rect = cardListContainer.GetComponent<RectTransform>();
                if (rect) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
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
            RebuildCardList();
        }

        public void LoadCards(InfoCardData[] cards)
        {
            availableCards.Clear();
            availableCards.AddRange(cards);
            Debug.Log($"[InfoCardManager] LoadCards: {cards.Length}개 카드 로드, active={gameObject.activeInHierarchy}");
            RebuildCardList();
        }

        public bool TryAutoShow(string waypointId)
        {
            foreach (var card in availableCards)
            {
                if (card.autoShow && card.triggerWaypointId == waypointId)
                {
                    ShowCard(card, true);
                    return true;
                }
            }
            return false;
        }

        public void ShowCard(InfoCardData card, bool autoShown)
        {
            activeCard = card;
            cardOpenTime = Time.time;

            if (cardListContainer) cardListContainer.SetActive(false);

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

            // GlassOnly: 카드 리스트 뷰 대신 탭 전체 숨김 (레이캐스트 차단 방지 + 불필요한 UI 제거)
            if (ConditionController.Instance != null
                && ConditionController.Instance.CurrentCondition == ExperimentCondition.GlassOnly)
            {
                BeamProHubController.Instance?.HideAllTabs();
            }
            else
            {
                ShowListView();
            }
        }

        private void ShowListView()
        {
            if (cardListContainer) cardListContainer.SetActive(true);
            if (cardPanel) cardPanel.SetActive(false);
            if (comparisonCard) comparisonCard.gameObject.SetActive(false);
        }

        private void TryLoadFromCurrentMission()
        {
            var mission = MissionManager.Instance?.CurrentMission;
            if (mission?.infoCards != null && mission.infoCards.Length > 0)
            {
                availableCards.Clear();
                availableCards.AddRange(mission.infoCards);
                Debug.Log($"[InfoCardManager] Self-heal: {mission.infoCards.Length}개 카드 직접 로드");
            }
        }

        private void ShowEmptyState()
        {
            if (cardListEmptyText)
            {
                cardListEmptyText.text = LocalizationManager.Get("infocard.empty");
                cardListEmptyText.gameObject.SetActive(true);
            }
        }

        private void RebuildCardList()
        {
            if (!cardItemTemplate || !cardListContainer) return;

            foreach (var item in spawnedItems)
                if (item) Destroy(item);
            spawnedItems.Clear();

            var parent = cardItemTemplate.transform.parent;

            if (cardListEmptyText)
            {
                cardListEmptyText.text = LocalizationManager.Get("infocard.empty");
                cardListEmptyText.gameObject.SetActive(availableCards.Count == 0);
            }

            foreach (var card in availableCards)
            {
                var item = Instantiate(cardItemTemplate, parent);
                item.SetActive(true);

                var titleText = item.transform.Find("CardItemTitle")?.GetComponent<TextMeshProUGUI>();
                if (titleText) titleText.text = card.GetTitle();

                var locationText = item.transform.Find("CardItemLocation")?.GetComponent<TextMeshProUGUI>();
                if (locationText) locationText.text = card.triggerWaypointId ?? "";

                var btn = item.GetComponent<Button>();
                if (btn)
                {
                    var capturedCard = card;
                    btn.onClick.AddListener(() => ShowCard(capturedCard, false));
                }

                spawnedItems.Add(item);
            }
        }
    }
}
