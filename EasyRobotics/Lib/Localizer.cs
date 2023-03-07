using KSP.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace EasyRobotics
{
    public static class LocalizationManager
    {
        private const string LOC_PREFIX = "EASYROBOTICS";
        private const string SETTINGS_NODE = "EASY_ROBOTICS_SETTINGS";
        private const string MOD_FOLDER = "EasyRobotics";

        private static string _modPath;
        /// <summary>
        /// return the "GameData/ModName" directory, assuming a "GameData/ModName/Plugins/plugin.dll" structure.
        /// </summary>
        public static string ModPath
        {
            get
            {
                if (_modPath == null)
                {
                    _modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    while (true)
                    {
                        if (string.IsNullOrEmpty(_modPath))
                            throw new Exception($"Mod install directory not found from {Assembly.GetExecutingAssembly().Location}");

                        string parentDirPath = Path.GetDirectoryName(_modPath);

                        if (Path.GetFileName(parentDirPath) == "GameData")
                            break;

                        _modPath = parentDirPath;
                    }
                }

                return _modPath;
            }
        }

        public static void ModuleManagerPostLoad()
        {
            ParseLocalization();
        }

        private static void ParseLocalization()
        {
            if (Localizer.CurrentLanguage == "en-us")
                return;

            Type stringType = typeof(string);

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (FieldInfo staticField in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (staticField.FieldType != stringType)
                        continue;

                    string name = staticField.Name;
                    if (name.StartsWith("LOC_", StringComparison.Ordinal))
                    {
                        string tag = $"#{LOC_PREFIX}_{name.Substring(4)}";
                        if (Localizer.Tags.ContainsKey(tag))
                            staticField.SetValue(null, Localizer.Format(tag));
                    }
                    else if (name.StartsWith("AUTOLOC_", StringComparison.Ordinal))
                    {
                        string tag = (string)staticField.GetValue(null);
                        if (Localizer.Tags.ContainsKey(tag))
                            staticField.SetValue(null, Localizer.Format(tag));
                    }
                }
            }
        }

        private static void GenerateLocTemplateIfRequested()
        {
            UrlDir.UrlConfig[] settingsNode = GameDatabase.Instance.GetConfigs(SETTINGS_NODE);

            string generateLocTemplate = null;
            if (settingsNode == null || settingsNode.Length != 1 || !settingsNode[0].config.TryGetValue("generateLocalizationTemplate", ref generateLocTemplate))
                return;

            try
            {
                GenerateLocTemplate(generateLocTemplate);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to generate localization file : {e}");
            }
        }

        public static void GenerateLocTemplate(string langCode = "en-us")
        {
            bool isEnglishLoc = langCode == "en-us";

            Dictionary<string, string> langLoc = null;
            if (!isEnglishLoc)
            {
                ConfigNode locNode = null;
                UrlDir.UrlConfig[] locConfigs = GameDatabase.Instance.GetConfigs("Localization");
                foreach (UrlDir.UrlConfig locConfig in locConfigs)
                {
                    if (!locConfig.url.StartsWith(MOD_FOLDER))
                        continue;

                    if (!locConfig.config.TryGetNode(langCode, ref locNode))
                        continue;

                    langLoc = new Dictionary<string, string>(locNode.values.Count);

                    foreach (ConfigNode.Value value in locNode.values)
                    {
                        string valueString = value.value.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                        langLoc.Add(value.name, valueString);
                    }

                    break;
                }
            }

            string tab = "  ";

            List<string> lines = new List<string>();
            lines.Add("Localization");
            lines.Add("{");
            lines.Add(tab + langCode);
            lines.Add(tab + "{");

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                bool headerAdded = false;
                foreach (FieldInfo staticField in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (staticField.Name.StartsWith("LOC_", StringComparison.Ordinal))
                    {
                        if (!headerAdded)
                        {
                            lines.Add(string.Empty);
                            lines.Add(tab + tab + "// " + type.Name);
                            lines.Add(string.Empty);
                            headerAdded = true;
                        }

                        
                        string configValueName = $"#{LOC_PREFIX}_{staticField.Name.Substring(4)}";
                        string line = tab + tab + configValueName + " = ";

                        string englishValue = (string)staticField.GetValue(null);
                        englishValue = englishValue.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

                        if (isEnglishLoc)
                        {
                            line += englishValue;
                        }
                        else
                        {
                            lines.Add(string.Empty);
                            lines.Add(tab + tab + "// " + englishValue);

                            if (langLoc != null && langLoc.Count > 0)
                            {
                                if (langLoc.TryGetValue(configValueName, out string translatedString))
                                    line += translatedString;
                                else
                                    line += "MISSINGLOC";
                            }
                        }

                        lines.Add(line);
                    }
                }
            }

            lines.Add(tab + "}");
            lines.Add("}");


            string path = Path.Combine(ModPath, "Localization", $"{langCode}.cfg.generatedLoc");
            File.WriteAllLines(path, lines);
            UnityEngine.Debug.Log($"[{MOD_FOLDER}] Localization file generated: \"{path}\"");
            ScreenMessages.PostScreenMessage($"KSP Community Fixes\nLocalization file generated\n\"{path}\"", 60f, ScreenMessageStyle.UPPER_LEFT);
        }
    }
}
