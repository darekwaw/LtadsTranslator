using LtadsTranslator.Enums;
using LtadsTranslator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LtadsTranslator
{
    public class Translator
    {
        private readonly List<SystemLanguage> _systemLanguages;
        private List<CollSl> _expressionsList;
        private List<LtadsDictionary> _dictionary;

        public Translator(List<SystemLanguage> systemLanguages, List<LtadsDictionary> dictionary)
        {
            _systemLanguages = systemLanguages;
            _dictionary = dictionary;
        }

        public List<Translation> Translate(string sentence, Language sourceLanguage)
        {
            var translationCollection = new List<Translation> { };

            foreach (Translation translation in CreateTranslationCollection(sentence))
            {
                translationCollection.Add(InternalTranslate(translation, sourceLanguage, translation.Language));
            }

            return translationCollection;
        }

        public List<Translation> Translate(string sentence, Language sourceLanguage, Language destinationLanguage)
        {
            var transtateCollection = new List<Translation> { };
            Translation translation = new Translation
            {
                Language = sourceLanguage,
                Name = RemoveWhitespace(sentence),
                NameToTranslate = RemoveWhitespace(sentence),
                TranslatedName = RemoveWhitespace(sentence)
            };
            transtateCollection.Add(InternalTranslate(translation, sourceLanguage, destinationLanguage));
            return transtateCollection;
        }

        private Translation InternalTranslate(Translation translation, Language sourceLanguage, Language destinationLanguage)
        {
            if (sourceLanguage == destinationLanguage)
                return new Translation
                {
                    Language = sourceLanguage,
                    NameToTranslate = string.Empty,
                    Status = TranslationStatus.Translated
                };

            #region Kasowanie znaków dialektycznych
            RemoveLocaleCharacters(translation);
            #endregion

            #region Generowanie słowników ze słowami
            List<string> ListWords = translation.NameToTranslate.Split(' ').Where(x => x != "").ToList();

            int maxWords = ListWords.Count();
            if (maxWords == 0)
            {
                return translation;
            }
            #endregion

            #region Generowanie wyrażeń do tłumaczenia
            _expressionsList = new List<CollSl>();
            GetExpressionsList(ListWords);
            _expressionsList = _expressionsList.OrderByDescending(x => x.wyrazenie.Length).ToList();

            IEnumerable<T> listaPrzetlumaczona = null;
            List<string> ListSentence = _expressionsList.Select(x => x.wyrazenie).ToList();

            listaPrzetlumaczona = from TRN in _dictionary
                                  join sentenceToTranslate in ListSentence on TRN.PL equals sentenceToTranslate
                                  select new T
                                  {
                                      W1 = sentenceToTranslate,
                                      W2 = FindInDictionary(sentenceToTranslate, sourceLanguage, destinationLanguage)
                                  };
            #endregion

            #region Tłumaczenie
            bool trn = false;   //Czy o do próby tłumaczenia
            try
            {
                var lp = listaPrzetlumaczona.OrderByDescending(s => s.W1.Length).ToList();

                foreach (var sl in listaPrzetlumaczona.OrderByDescending(s => s.W1.Length))
                {
                    if (ContainsTest(translation.NameToTranslate, sl.W1))
                    {
                        trn = true;

                        translation.TranslatedName = ReplaceWholeWord(translation.TranslatedName, sl.W1.ToUpperInvariant(), sl.W2.ToUpperInvariant());
                        translation.NameToTranslate = ReplaceWholeWord(translation.NameToTranslate, sl.W1.ToUpperInvariant(), "");
                    }
                }

                translation.NameToTranslate = Oczysc(translation.NameToTranslate);

                if (string.IsNullOrEmpty(translation.NameToTranslate))
                {
                    translation.Status = TranslationStatus.Translated;
                }
                else
                {
                    translation.Status = TranslationStatus.NotTranslated;
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Błąd tłumaczenia aukcji", ex);
            }
            #endregion

            if (translation.Status == TranslationStatus.Translated)
            {
                return translation;
            }

            if (!trn)
            {
                translation.Status = TranslationStatus.NotTranslated;
                return translation;
            }

            return InternalTranslate(translation, sourceLanguage, destinationLanguage);
        }

        private string Oczysc(string TL)
        {
            string oryginal = TL;
            try
            {
                TL = TL.Replace("*", "");

                TL = RemoveWhitespace(TL);

                List<OczyscStrukt> P = new List<OczyscStrukt>();
                P.Add(new OczyscStrukt(@"\b\w+\-\d+\b", true));                                               //Zawiera kod np C-20
                P.Add(new OczyscStrukt("\\b\\w+\\d+\\,{0,1}.{0,1}[a-zA-Z][/-][a-zA-Z]\\b", true));            //Zawiera kod produktu np AK76OP/AK...
                P.Add(new OczyscStrukt("\\b\\w+\\d+\\,{0,1}.{0,1}[a-zA-Z]\\b", true));                             //Zawiera kod produktu np AK76OP
                P.Add(new OczyscStrukt("\\b\\w+\\d+\\,{0,1}.{0,1}\\b", true));                                         //Zawiera kod produktu np AK760 lub AK760.
                P.Add(new OczyscStrukt("\\b\\w\\d+\\b", true));                                                                 //Zawiera kod A8888
                                                                                                                                //P.Add(New OczyscStrukt("\b\w\d+\b", True))                        'Zawiera kod @
                P.Add(new OczyscStrukt("\\d+\\,{0,1}.{0,1}\\d+%", true));                                               //Zawiera liczbę z % (może być dziesiętna)   2,5%    lub 2.5%
                P.Add(new OczyscStrukt("\\b\\d+\\,{0,1}.{0,1}\\b", true));                                               //Zawiera liczbę (może być dziesiętna)       1,9     lub 1.9
                P.Add(new OczyscStrukt("\\b\\d+\\b", true));                                                                      //Zawiera same cyfry
                P.Add(new OczyscStrukt("\\s+$", false));                                                                            //Kończy się więcej niż jedną spacją
                P.Add(new OczyscStrukt("^\\b.\\b$", true));                                                                             //Została jedna litera do przetłumaczenia
                P.Add(new OczyscStrukt("\\b\\d+[a-zA-Z]+", true));
                P.Add(new OczyscStrukt("\\b.+\\d\\w+", true));                                                                      //Zawiera kod produktu AK07XXXX (znaki z przodu + cyfry + dokończenie słowa z tyłu)
                P.Add(new OczyscStrukt(@"^[\.\s?/+_\-@!\(\),\,,<>'&#*^%=;:\[\]~`\$\%\^\/]{1,}$", false)); //Zawiera wyłącznie znaki wymienione w [.....]
                P.Add(new OczyscStrukt(@"[|]", false)); //Zawiera wyłącznie znaki wymienione w [.....]

                foreach (OczyscStrukt Wzor in P)
                {

                    if (Regex.Matches(TL, Wzor.wzor).Count > 0)
                    {
                        TL = Regex.Replace(TL, Wzor.wzor, "");
                    }

                    TL = RemoveWhitespace(TL);
                }

                TL = RemoveWhitespace(TL);

                if (TL != oryginal)
                {
                    Oczysc(TL);
                }

                P.Clear();
                P = null;
            }
            catch (StackOverflowException ex)
            {
                EventLog log = new EventLog
                {
                    EventTime = DateTime.UtcNow,
                    EventType = 1,
                    ModuleName = "Translator",
                    ItemName = $"Błąd oczyszczania stringa {oryginal}{Environment.NewLine}{ex.GetBaseException().Message}"
                };

                //eventLogService.Insert(log);
            }
            catch (Exception ex)
            {
                EventLog log = new EventLog
                {
                    EventTime = DateTime.UtcNow,
                    EventType = 1,
                    ModuleName = "Translator",
                    ItemName = $"Błąd oczyszczania stringa {oryginal}{Environment.NewLine}{ex.GetBaseException().Message}"
                };

                //eventLogService.Insert(log);
            }

            return TL;
        }
        private String ReplaceWholeWord(String s, String word, String bywhat)
        {
            string input = s;
            try
            {
                word = word.Replace("+", @"\+");
                word = word.Replace("(", "");
                word = word.Replace(")", "");
                word = word.Replace("[", @"\[");
                word = word.Replace("]", @"\]");
                word = word.Replace(".", @"\.");

                string pattern = string.Format(@"\b{0}\b", word);// string.Format( @"(?<=[^A-Za-z]|^)\{0}(?=[^A-Za-z]|$)", word);
                string pattern2 = "(?<!\\S\\()" + word + "(?!\\S\\))";
                string replace = bywhat;
                string result = Regex.Replace(input, pattern2, replace);
                return result;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private bool ContainsTest(string nameToTranslate, string w1)
        {
            nameToTranslate = nameToTranslate.Replace("/", "");
            w1 = w1.Replace("+", @"\+");
            w1 = w1.Replace("[", @"\[");
            w1 = w1.Replace("]", @"\]");

            Regex r = new Regex(w1.Replace("/", ""), RegexOptions.IgnoreCase);
            Match m = r.Match(nameToTranslate);

            return m.Success;
        }
        private string FindInDictionary(string sentenceToTranslate, Language sourceLanguage, Language destinationLanguage)
        {
            string result = string.Empty;
            IEnumerable<LtadsDictionary> foundDictionaries = null;

            switch (sourceLanguage)
            {
                case Language.PL:
                    foundDictionaries = _dictionary.Where(x => x.PL == sentenceToTranslate);
                    break;
                case Language.LT:
                    foundDictionaries = _dictionary.Where(x => x.LT == sentenceToTranslate);
                    break;
                default:
                    break;
            }

            if (foundDictionaries != null)
            {
                switch (destinationLanguage)
                {
                    case Language.PL:
                        result = foundDictionaries.FirstOrDefault()?.PL;
                        break;
                    case Language.LT:
                        result = foundDictionaries.FirstOrDefault()?.LT;
                        break;
                    default:
                        break;
                }
            }

            return result;

        }
        private void GetExpressionsList(List<string> listWords)
        {
            string expressions = null;

            for (int i1 = 0; i1 < listWords.Count(); i1++)
            {
                if (!string.IsNullOrEmpty(expressions))
                {
                    expressions += " ";
                }

                expressions += listWords[i1];
                _expressionsList.Add(new CollSl(expressions, 1, expressions.Length));
            }

            if (listWords.Count() > 0)
            {
                listWords.RemoveAt(0);
                GetExpressionsList(listWords);
            }
        }
        private List<Translation> CreateTranslationCollection(string sentence)
        {
            var translations = new List<Translation> { };

            foreach (var systemLanguage in _systemLanguages.Where(x => x.IsActive))
            {
                translations.Add(new Translation
                {
                    Language = systemLanguage.Language,
                    Name = RemoveWhitespace(sentence),
                    NameToTranslate = RemoveWhitespace(sentence),
                    TranslatedName = RemoveWhitespace(sentence)
                });
            }

            return translations;
        }
        private string RemoveWhitespace(string sentence)
        {
            var str = sentence.ToUpper().Trim();
            try
            {
                str = str.Replace("?", "");
                str = str.Replace("(", "");
                str = str.Replace(")", "");

                str = Regex.Replace(str, " {2,}", " ");
            }
            catch (Exception)
            {
                Console.WriteLine("s");
            }

            return str;
        }
        private void RemoveLocaleCharacters(Translation translation)
        {
            switch (translation.Language)
            {
                case Language.PL:
                    translation.Name = RemovePL(translation.Name);
                    break;
                case Language.LT:
                    translation.Name = RemoveLT(translation.Name);
                    break;
                default:
                    break;
            }

        }
        private string RemovePL(string sentence)
        {
            sentence = sentence.ToUpper();
            sentence = sentence.Replace("Ą", "A");
            sentence = sentence.Replace("Ę", "E");
            sentence = sentence.Replace("Ł", "L");
            sentence = sentence.Replace("Ó", "O");
            sentence = sentence.Replace("Ś", "S");
            sentence = sentence.Replace("Ć", "C");
            sentence = sentence.Replace("Ź", "Z");
            sentence = sentence.Replace("Ż", "Z");
            sentence = sentence.Replace("Ń", "N");
            sentence = sentence.Replace("*", " ");

            return sentence;
        }
        private string RemoveLT(string sentence)
        {
            sentence = sentence.ToUpper();
            sentence = sentence.Replace("Ž", "Z");
            sentence = sentence.Replace("Ė", "E");
            sentence = sentence.Replace("Č", "C");
            sentence = sentence.Replace("Ų", "U");
            sentence = sentence.Replace("Ū", "U");
            sentence = sentence.Replace("Š", "S");
            sentence = sentence.Replace("Į", "I");
            sentence = sentence.Replace("*", " ");
            return sentence;
        }

    }

    internal struct CollSl
    {
        public string wyrazenie { get; set; }
        public int ileSlow { get; set; }
        public int ileZnakow { get; set; }

        public CollSl(string Wyrazenie, int IleSlow, int IleZnakow)
            : this()
        {
            this.wyrazenie = Wyrazenie;
            this.ileSlow = IleSlow;
            this.ileZnakow = ileZnakow;
        }
    }
    internal struct OczyscStrukt
    {
        public string wzor { get; set; }
        public bool licz { get; set; }

        public OczyscStrukt(string someVal, bool otherVal)
            : this()
        {
            this.wzor = someVal;
            this.licz = otherVal;
        }
    }

    internal class T
    {
        public T()
        { }

        public T(string Z1, string Z2)
        {
            this.W1 = Z1;
            this.W2 = Z2;
        }
        public string W1 { get; set; }
        public string W2 { get; set; }
    }

}
