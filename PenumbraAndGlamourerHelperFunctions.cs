using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using PenumbraAndGlamourerHelpers.IPC.ThirdParty.Glamourer;
using PenumbraAndGlamourerHelpers.IPC.ThirdParty.Glamourer.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.IoC;
using System.Text.RegularExpressions;
using System.IO;

namespace PenumbraAndGlamourerHelpers
{
    public static class PenumbraAndGlamourerHelperFunctions
    {
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        public static string ModelRaceToRaceCode(int race, int clan, int gender)
        {
            // Formula: ((Race - 1) * 4) + ((Clan - 1) * 2) + Gender + 1
            int raceCodeInt = (race * 4) + (clan * 2) + gender + 1;
            return raceCodeInt.ToString("D4");
        }

        public static string SubRaceToSubRaceName(int race, int clan)
        {
            if (race == 5) return clan == 0 ? "raen" : "xaela";
            return "";
        }

        public static void WearOutfit(EquipObject item, Guid collection, int objectIndex, ICollection<string> modelMods, ref bool blockDataRefreshes)
        {
            blockDataRefreshes = true;
            if (collection == Guid.Empty)
            {
                collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(objectIndex).Item3.Id;
            }
            SetClothingMod(item.Name, modelMods, collection, false);
            SetDependancies(item.Name, modelMods, collection, false);
            PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(objectIndex, RedrawType.Redraw);
            SetEquipment(item, objectIndex);
            blockDataRefreshes = false;
        }

        public static bool SetEquipment(EquipObject equipItem, int objectIndex)
        {
            bool changed = false;
            var result = PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, FullEquipTypeToApiEquipSlot(equipItem.Type), equipItem.ItemId.Id, new List<byte>());
            changed = true;
            return changed;
        }

        public static bool SetEquipmentRaw(FullEquipType equipItem, ulong itemId, int objectIndex)
        {
            bool changed = false;
            var result = PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, FullEquipTypeToApiEquipSlot(equipItem), itemId, new List<byte>());
            changed = true;
            return changed;
        }

        public static ApiEquipSlot FullEquipTypeToApiEquipSlot(FullEquipType fullEquipType)
        {
            switch (fullEquipType)
            {
                case FullEquipType.Unknown: return ApiEquipSlot.Unknown;
                case FullEquipType.Head: return ApiEquipSlot.Head;
                case FullEquipType.Body: return ApiEquipSlot.Body;
                case FullEquipType.Hands: return ApiEquipSlot.Hands;
                case FullEquipType.Legs: return ApiEquipSlot.Legs;
                case FullEquipType.Feet: return ApiEquipSlot.Feet;
                case FullEquipType.Ears: return ApiEquipSlot.Ears;
                case FullEquipType.Wrists: return ApiEquipSlot.Wrists;
                case FullEquipType.Finger: return ApiEquipSlot.RFinger;
                default: return ApiEquipSlot.Unknown;
            }
        }

        public static CharacterCustomization GetCustomization(ICharacter playerCharacter)
        {
            try
            {
                var result = PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex);
                if (result.Item1 == 0) // Success
                {
                    return CharacterCustomization.ReadCustomization(result.Item2);
                }
                else
                {
                    DragAndDropTexturing.Plugin.Log.Error($"[Drag And Drop Debug] Glamourer GetStateBase64 failed with result: {result.Item1}");
                }
            }
            catch (Exception ex)
            {
                DragAndDropTexturing.Plugin.Log.Error($"[Drag And Drop Debug] Error in GetCustomization: {ex.Message}");
            }

            DragAndDropTexturing.Plugin.Log.Information("[Drag And Drop Debug] Falling back to manual customization detection.");
            return new CharacterCustomization()
            {
                Customize = new Customize()
                {
                    EyeColorLeft = new FacialValue() { Value = playerCharacter.Customize.Length > 9 ? playerCharacter.Customize[9] : (byte)0 },
                    EyeColorRight = new FacialValue() { Value = playerCharacter.Customize.Length > 15 ? playerCharacter.Customize[15] : (byte)0 },
                    BustSize = new BustSize() { Value = playerCharacter.Customize.Length > 24 ? playerCharacter.Customize[24] : (byte)0 },
                    LipColor = new LipColor() { Value = playerCharacter.Customize.Length > 20 ? playerCharacter.Customize[20] : (byte)0 },
                    Gender = new Gender() { Value = playerCharacter.Customize.Length > 1 ? playerCharacter.Customize[1] : (byte)0 },
                    Height = new Height() { Value = playerCharacter.Customize.Length > 3 ? playerCharacter.Customize[3] : (byte)0 },
                    Clan = new Clan() { Value = playerCharacter.Customize.Length > 4 ? playerCharacter.Customize[4] : (byte)0 },
                    Face = new FacialValue() { Value = playerCharacter.Customize.Length > 5 ? playerCharacter.Customize[5] : (byte)0 },
                    Race = new Race() { Value = playerCharacter.Customize.Length > 0 ? playerCharacter.Customize[0] : (byte)0 },
                    BodyType = new BodyType() { Value = playerCharacter.Customize.Length > 2 ? playerCharacter.Customize[2] : (byte)0 }
                }
            };
        }

        public static void SetClothingMod(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true)
        {
            foreach (string modName in modelMods)
            {
                if (modName.ToLower().Contains(modelMod.ToLower()))
                {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                }
                else if (CheckIfValidToChange(modName, modelMods))
                {
                    if (disableOtherMods)
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                    else
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                }
            }
        }

        public static void SetDependancies(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true)
        {
            foreach (string modName in modelMods)
            {
                if (modName.ToLower().Contains(modelMod.ToLower()))
                {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                }
                else if (FindStringMatch(modelMod, modName))
                {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 10);
                }
                else if (disableOtherMods && CheckIfValidToChange(modName, modelMods))
                {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                }
                else if (CheckIfValidToChange(modName, modelMods))
                {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                }
            }
        }

        public static bool CheckIfValidToChange(string mod, ICollection<string> modelMods)
        {
            var items = PenumbraAndGlamourerIpcWrapper.Instance.GetChangedItemsForMod.Invoke("", mod).Values;
            foreach (var changedItem in items)
            {
                try
                {
                    string equipItemJson = JsonConvert.SerializeObject(changedItem);
                    if (equipItemJson.Length > 200)
                    {
                        var equipObject = JsonConvert.DeserializeObject<EquipObject>(equipItemJson);
                        switch (equipObject.ItemId.Id)
                        {
                            case 9292: case 9293: case 9294: case 9295:
                            case 10032: case 10033: case 10034: case 10035: case 10036:
                            case 13775: case 0:
                                return false;
                        }
                    }
                }
                catch { }
            }
            return true;
        }

        public static bool FindStringMatch(string sourceMod, string comparisonMod)
        {
            string[] strings = sourceMod.Split(' ');
            foreach (string value in strings)
            {
                string loweredValue = value.ToLower();
                if (comparisonMod.ToLower().Contains(loweredValue) && loweredValue.Length > 4 && !loweredValue.Contains("[") && !loweredValue.Contains("]")
                  && !loweredValue.Contains("by") && !loweredValue.Contains("update") && !loweredValue.Contains("megapack") && !comparisonMod.Contains("megapack"))
                    return true;
            }
            return false;
        }

        public static void CleanSlate(Guid collection, ICollection<string> modelMods)
        {
            if (collection == Guid.Empty) collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(0).EffectiveCollection.Id;
            foreach (string modName in modelMods)
            {
                if (CheckIfValidToChange(modName, modelMods))
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, "", false, modName);
            }
        }

        public static int DetectBaseBodyFromPenumbra(Guid collectionId, out string detectedModName)
        {
            detectedModName = "";
            try
            {
                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                List<(string Name, string Dir, int Priority)> activeMods = new List<(string Name, string Dir, int Priority)>();

                foreach (var mod in mods)
                {
                    string lowerKey = mod.Key.ToLower();
                    string lowerValue = mod.Value.ToLower();
                    if (lowerValue.Contains("drag and drop") || lowerKey.Contains("drag and drop") || lowerValue.Contains("loosetexturecompilerdlc") || lowerKey.Contains("loosetexturecompilerdlc")) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == Penumbra.Api.Enums.PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1 == true)
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2));
                }

                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                foreach (var mod in activeMods)
                {
                    string defaultJsonPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, "default_mod.json");
                    if (System.IO.File.Exists(defaultJsonPath))
                    {
                        string json = System.IO.File.ReadAllText(defaultJsonPath);
                        if (CheckIfJsonIsBodyType(mod.Name, mod.Dir, json, out int type)) { detectedModName = mod.Name; return type; }
                    }
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(modDirectoryPath, mod.Dir)))
                    {
                        foreach (var file in System.IO.Directory.GetFiles(System.IO.Path.Combine(modDirectoryPath, mod.Dir), "group_*.json"))
                        {
                            string json = System.IO.File.ReadAllText(file);
                            if (CheckIfJsonIsBodyType(mod.Name, mod.Dir, json, out int type)) { detectedModName = mod.Name; return type; }
                        }
                    }
                }
            }
            catch (Exception ex) { Log?.Warning(ex, "Failed to detect base body from Penumbra"); }
            return -1;
        }

        private static bool CheckIfJsonIsBodyType(string name, string dir, string json, out int type)
        {
            type = 2;
            string lowerName = name.ToLower();
            string lowerDir = dir.ToLower();
            string lowerJson = json.ToLower();
            if (lowerName.Contains("gen3") || lowerName.Contains("eve") || lowerDir.Contains("gen3") || lowerJson.Contains("gen3") || lowerJson.Contains("eve")) { type = 2; return true; }
            if (lowerName.Contains("tbse") || lowerDir.Contains("tbse") || lowerJson.Contains("tbse")) { type = 3; return true; }
            if (lowerName.Contains("yab") || lowerDir.Contains("yab") || lowerJson.Contains("yab") || lowerName.Contains("bibo") || lowerName.Contains("b+") || lowerDir.Contains("bibo") || lowerJson.Contains("bibo")) { type = 1; return true; }
            return false;
        }

        public static void ExtractActiveTextureFromPenumbra(Guid collectionId, string category, string raceCode, string subRaceName, out string extractedModName, out string extractedBase, out string extractedNormal, out string extractedMask, DragAndDropTexturing.Plugin plugin, FFXIVLooseTextureCompiler.PathOrganization.TextureSet item = null)
        {
            extractedModName = ""; extractedBase = ""; extractedNormal = ""; extractedMask = "";
            if (item == null) return;

            try
            {
                plugin.Chat.Print($"[Drag And Drop Debug] Searching for {category} texture. RaceCode: {raceCode}");

                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();

                List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)>();
                foreach (var mod in mods)
                {
                    // SKIP OUR OWN MODS to avoid detecting ourselves as the underlay
                    // We check for "Drag And Drop" but also "do_not_edit" which is common in generated mods
                    if (mod.Value.Contains("Drag And Drop") || mod.Key.Contains("Drag And Drop") || mod.Key.Contains("do_not_edit")) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1)
                    {
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                    }
                }
                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                foreach (var mod in activeMods)
                {
                    Dictionary<string, string> files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);

                    // Try to find the paths from the item in this mod
                    if (!string.IsNullOrEmpty(item.InternalBasePath) && files.TryGetValue(item.InternalBasePath, out string baseMatch))
                    {
                        // Skip generated files
                        if (baseMatch.Contains("do_not_edit") || baseMatch.Contains("_generated")) continue;

                        string fullPath = Path.Combine(modDirectoryPath, mod.Dir, baseMatch.Replace("/", "\\"));
                        if (File.Exists(fullPath))
                        {
                            extractedBase = fullPath;
                            extractedModName = mod.Name;

                            // Get Normal and Mask from SAME mod if they exist
                            if (!string.IsNullOrEmpty(item.InternalNormalPath) && files.TryGetValue(item.InternalNormalPath, out string normMatch))
                                extractedNormal = Path.Combine(modDirectoryPath, mod.Dir, normMatch.Replace("/", "\\"));

                            if (!string.IsNullOrEmpty(item.InternalMaskPath) && files.TryGetValue(item.InternalMaskPath, out string maskMatch))
                                extractedMask = Path.Combine(modDirectoryPath, mod.Dir, maskMatch.Replace("/", "\\"));

                            plugin.Chat.Print($"[Drag And Drop Debug] Found base: {item.InternalBasePath} in mod {mod.Name}");
                            return;
                        }
                    }
                }

                plugin.Chat.Print($"[Drag And Drop Debug] No modded {category} texture found (excluding 'Drag And Drop' mods).");
            }
            catch (Exception ex)
            {
                plugin.Chat.Print($"[Drag And Drop Debug] ERROR in ExtractActiveTextureFromPenumbra: {ex.Message}");
            }
        }

        private static Dictionary<string, string> GetFilesForMod(string modDirectory, string modDir, Dictionary<string, List<string>> settings)
        {
            Dictionary<string, string> files = new Dictionary<string, string>();
            string modPath = Path.Combine(modDirectory, modDir);

            // Default mod files
            string defaultJson = Path.Combine(modPath, "default_mod.json");
            if (File.Exists(defaultJson))
            {
                try
                {
                    var modData = JsonConvert.DeserializeObject<PenumbraModData>(File.ReadAllText(defaultJson));
                    if (modData?.Files != null) foreach (var kvp in modData.Files) files[kvp.Key] = kvp.Value;
                }
                catch { }
            }

            // Group files based on settings
            if (Directory.Exists(modPath))
            {
                foreach (var groupFile in Directory.GetFiles(modPath, "group_*.json"))
                {
                    try
                    {
                        var groupData = JsonConvert.DeserializeObject<PenumbraGroupData>(File.ReadAllText(groupFile));
                        if (groupData != null && settings.TryGetValue(groupData.Name, out var activeOptions))
                        {
                            foreach (var option in groupData.Options)
                            {
                                if (activeOptions.Contains(option.Name) && option.Files != null)
                                {
                                    foreach (var kvp in option.Files) files[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            return files;
        }

        private class PenumbraModData { public Dictionary<string, string> Files { get; set; } }
        private class PenumbraGroupData
        {
            public string Name { get; set; }
            public List<PenumbraOptionData> Options { get; set; }
        }
        private class PenumbraOptionData
        {
            public string Name { get; set; }
            public Dictionary<string, string> Files { get; set; }
        }
    }
}
