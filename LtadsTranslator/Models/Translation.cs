using LtadsTranslator.Enums;

namespace LtadsTranslator.Models
{
    public class Translation
    {
        public string Name { get; set; }
        public string NameToTranslate { get; set; }
        public string TranslatedName { get; set; }
        public Language Language { get;  set; }
        public TranslationStatus Status { get; set; } = TranslationStatus.ForTranslation;
    }
}
