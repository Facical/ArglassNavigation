using System;

namespace ARNavExperiment.Mission
{
    [Serializable]
    public class ComparisonData
    {
        public string comparisonId;
        public string[] itemNames;
        public string[] attributes;
        public string[] values; // flattened matrix: values[itemIndex * attributes.Length + attrIndex]

        public string GetValue(int itemIndex, int attrIndex)
        {
            return values[itemIndex * attributes.Length + attrIndex];
        }
    }
}
