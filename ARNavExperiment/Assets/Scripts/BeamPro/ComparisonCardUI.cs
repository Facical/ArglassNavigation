using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Logging;
using ARNavExperiment.Mission;

namespace ARNavExperiment.BeamPro
{
    public class ComparisonCardUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform tableContainer;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private Button closeButton;

        private ComparisonData activeData;
        private float showTime;

        private void Start()
        {
            closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void Show(ComparisonData data, string title)
        {
            activeData = data;
            showTime = Time.time;

            if (titleText) titleText.text = title;

            // clear existing rows
            if (tableContainer)
            {
                for (int i = tableContainer.childCount - 1; i >= 0; i--)
                    Destroy(tableContainer.GetChild(i).gameObject);
            }

            if (rowPrefab == null || tableContainer == null || data == null) return;

            // header row
            var headerRow = Instantiate(rowPrefab, tableContainer);
            var headerTexts = headerRow.GetComponentsInChildren<TextMeshProUGUI>();
            if (headerTexts.Length >= data.itemNames.Length + 1)
            {
                headerTexts[0].text = "";
                for (int i = 0; i < data.itemNames.Length; i++)
                    headerTexts[i + 1].text = data.itemNames[i];
            }

            // data rows
            for (int a = 0; a < data.attributes.Length; a++)
            {
                var row = Instantiate(rowPrefab, tableContainer);
                var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= data.itemNames.Length + 1)
                {
                    texts[0].text = data.attributes[a];
                    for (int i = 0; i < data.itemNames.Length; i++)
                        texts[i + 1].text = data.GetValue(i, a);
                }
            }

            EventLogger.Instance?.LogEvent("BEAM_COMPARISON_VIEWED",
                beamContentType: "comparison",
                extraData: $"{{\"comparison_id\":\"{data.comparisonId}\"}}");

            gameObject.SetActive(true);
        }
    }
}
