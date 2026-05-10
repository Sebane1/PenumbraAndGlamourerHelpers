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
using FFXIVLooseTextureCompiler.Export;
using FFXIVLooseTextureCompiler.Racial;

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
                            case 9292:
                            case 9293:
                            case 9294:
                            case 9295:
                            case 10032:
                            case 10033:
                            case 10034:
                            case 10035:
                            case 10036:
                            case 13775:
                            case 0:
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

        public static List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> GetActiveMods(Guid collectionId)
        {
            var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
            List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)>();

            foreach (var mod in mods)
            {
                string lowerKey = mod.Key.ToLower();
                string lowerValue = mod.Value.ToLower();
                if (lowerValue.Contains("drag and drop") || lowerKey.Contains("drag and drop") || lowerValue.Contains("loosetexturecompilerdlc") || lowerKey.Contains("loosetexturecompilerdlc")) continue;

                var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                if (settings.Item1 == Penumbra.Api.Enums.PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1 == true)
                    activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
            }

            activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return activeMods;
        }

        public static int DetectBaseBodyFromPenumbra(Guid collectionId, out string detectedModName, DragAndDropTexturing.Plugin plugin = null)
        {
            detectedModName = "";
            try
            {
                var activeMods = GetActiveMods(collectionId);
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                plugin?.Chat?.Print($"[Drag And Drop Debug] DetectBaseBody: {activeMods.Count} active mods found.");

                foreach (var mod in activeMods)
                {
                    // Skip our own generated mods
                    string lnm = mod.Name.ToLower();
                    string ldr = mod.Dir.ToLower();
                    if (lnm.Contains("texture body") || lnm.Contains("texture face") || lnm.Contains("texture eyes") || lnm.Contains("texture eyebrows") || lnm.Contains("texture mod") || ldr.Contains("do_not_edit"))
                    {
                        plugin?.Chat?.Print($"[Drag And Drop Debug] Skipping own mod: '{mod.Name}'");
                        continue;
                    }

                    Dictionary<string, string> files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);
                    
                    // Count paths per body type — the dominant type has more paths
                    int biboCount = 0, gen3Count = 0, tbseCount = 0;
                    foreach (var key in files.Keys)
                    {
                        string lowerKey = key.ToLowerInvariant();
                        if (lowerKey.StartsWith("chara/bibo_")) biboCount++;
                        if (lowerKey.Contains("tfgen3")) gen3Count++;
                        if (lowerKey.Contains("_b_d") && lowerKey.Contains("obj/body")) tbseCount++;
                    }

                    int bodyTypeCount = (biboCount > 0 ? 1 : 0) + (gen3Count > 0 ? 1 : 0) + (tbseCount > 0 ? 1 : 0);

                    if (bodyTypeCount == 1)
                    {
                        detectedModName = mod.Name;
                        int result = biboCount > 0 ? 1 : gen3Count > 0 ? 2 : 3;
                        plugin?.Chat?.Print($"[Drag And Drop Debug] Body detected via paths: '{mod.Name}' -> type {result}");
                        return result;
                    }
                    else if (bodyTypeCount > 1)
                    {
                        // Multiple body types — check name first, then use whichever has more paths
                        detectedModName = mod.Name;
                        plugin?.Chat?.Print($"[Drag And Drop Debug] Multiple body types in '{mod.Name}' (bibo={biboCount}, gen3={gen3Count}, tbse={tbseCount}). Checking name...");
                        if (lnm.Contains("bibo") || lnm.Contains("yab") || lnm.Contains("b+") || ldr.Contains("bibo")) return 1;
                        if (lnm.Contains("gen3") || lnm.Contains("tight & firm") || lnm.Contains("eve") || ldr.Contains("gen3")) return 2;
                        if (lnm.Contains("tbse") || ldr.Contains("tbse")) return 3;
                        // Name didn't help — the type with the most paths is the primary one
                        int max = Math.Max(biboCount, Math.Max(gen3Count, tbseCount));
                        int dominant = gen3Count == max ? 2 : biboCount == max ? 1 : 3;
                        plugin?.Chat?.Print($"[Drag And Drop Debug] Name inconclusive. Dominant path count wins: type {dominant}");
                        return dominant;
                    }
                    else
                    {
                        // No body paths found — check mod name/dir as last resort
                        if (lnm.Contains("bibo") || lnm.Contains("yab") || lnm.Contains("b+") || ldr.Contains("bibo")) { detectedModName = mod.Name; plugin?.Chat?.Print($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> Bibo"); return 1; }
                        if (lnm.Contains("gen3") || lnm.Contains("tight & firm") || ldr.Contains("gen3")) { detectedModName = mod.Name; plugin?.Chat?.Print($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> Gen3"); return 2; }
                        if (lnm.Contains("tbse") || ldr.Contains("tbse")) { detectedModName = mod.Name; plugin?.Chat?.Print($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> TBSE"); return 3; }
                    }
                }
            }
            catch (Exception ex) { Log?.Warning(ex, "Failed to detect base body from Penumbra"); }
            plugin?.Chat?.Print("[Drag And Drop Debug] DetectBaseBody: No body mod found!");
            return -1;
        }

        public static void ExtractActiveTextureFromPenumbra(Guid collectionId, string category, string raceCode, string subRaceName, out string extractedModName, out string extractedBase, out string extractedNormal, out string extractedMask, DragAndDropTexturing.Plugin plugin, FFXIVLooseTextureCompiler.PathOrganization.TextureSet item = null)
        {
            extractedModName = ""; extractedBase = ""; extractedNormal = ""; extractedMask = "";
            if (item == null) return;

            try
            {
                plugin.Chat.Print($"[Drag And Drop Debug] Searching for {category} texture. RaceCode: {raceCode}, InternalBase: {item.InternalBasePath}");

                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                plugin.Chat.Print($"[Drag And Drop Debug] Total mods in list: {mods.Count}");

                List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)>();
                foreach (var mod in mods)
                {
                    // SKIP OUR OWN MODS
                    string modNameLower = mod.Value.ToLower();
                    string modDirLower = mod.Key.ToLower();
                    if (modNameLower.Contains("drag and drop") || modDirLower.Contains("drag and drop") || modDirLower.Contains("do_not_edit") ||
                        modNameLower.Contains("texture body") || modNameLower.Contains("texture face") || modNameLower.Contains("texture eyes") ||
                        modNameLower.Contains("texture eyebrows") || modNameLower.Contains("texture mod")) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1)
                    {
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                    }
                }
                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                plugin.Chat.Print($"[Drag And Drop Debug] Active mods (non-self): {activeMods.Count}");

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
                        else
                        {
                            plugin.Chat.Print($"[Drag And Drop Debug] Path matched in {mod.Name} but file not found: {fullPath}");
                        }
                    }
                }

                plugin.Chat.Print($"[Drag And Drop Debug] No modded {category} texture found for path: {item.InternalBasePath}");
            }
            catch (Exception ex)
            {
                plugin.Chat.Print($"[Drag And Drop Debug] ERROR in ExtractActiveTextureFromPenumbra: {ex.Message}");
            }
        }

        public static void PopulateOmniOverrides(Guid collectionId, int gender, int race, DragAndDropTexturing.Plugin plugin)
        {
            if (!BackupTexturePaths.OverrideMode) return;

            try
            {
                plugin.Chat.Print("[Drag And Drop Debug] Populating Omni Overrides...");
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();

                List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)>();
                foreach (var mod in mods)
                {
                    if (mod.Value.Contains("Drag And Drop") || mod.Key.Contains("Drag And Drop") || mod.Key.Contains("do_not_edit")) continue;
                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1)
                    {
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                    }
                }
                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                // Detect if character is redirected to another race (e.g. Hroth to Midlander)
                int mainRace = RaceInfo.SubRaceToMainRace(race);

                foreach (var mod in activeMods)
                {
                    Dictionary<string, string> files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);
                    plugin.Chat.Print($"[Drag And Drop Debug] Checking mod {mod.Name} for compatibility textures (Effective Race: {mainRace})...");

                    // Check Bibo+ (baseBody 1)
                    BackupTexturePaths.BiboOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 1, gender, mainRace, BackupTexturePaths.BiboOverride, plugin);
                    // Check Gen3 (baseBody 2)
                    BackupTexturePaths.Gen3Override = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 2, gender, mainRace, BackupTexturePaths.Gen3Override, plugin);
                    // Check Vanilla/Gen2 (baseBody 0)
                    BackupTexturePaths.Gen2Override = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 0, gender, mainRace, BackupTexturePaths.Gen2Override, plugin);
                    // Check TBSE (baseBody 3)
                    BackupTexturePaths.TbseOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 3, gender, mainRace, BackupTexturePaths.TbseOverride, plugin);
                    // Check Otopop (baseBody 5)
                    BackupTexturePaths.OtopopOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 5, gender, mainRace, BackupTexturePaths.OtopopOverride, plugin);
                    // Check Asym Lalafell (baseBody 6)
                    BackupTexturePaths.VanillaLalaOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 6, gender, mainRace, BackupTexturePaths.VanillaLalaOverride, plugin);
                    // Check Relala (baseBody 7)
                    BackupTexturePaths.RelalaOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, 7, gender, mainRace, BackupTexturePaths.RelalaOverride, plugin);
                }
                plugin.Chat.Print("[Drag And Drop Debug] Omni Overrides population check complete.");
            }
            catch (Exception ex)
            {
                plugin.Chat.Print($"[Drag And Drop Debug] ERROR in PopulateOmniOverrides: {ex.Message}");
            }
        }

        private static BackupTexturePaths CheckAndSetOverride(Dictionary<string, string> files, string modDir, string subDir, int baseBody, int gender, int race, BackupTexturePaths overrideField, DragAndDropTexturing.Plugin plugin)
        {
            // Only set if not already found by a higher priority mod
            string basePath = RacePaths.GetBodyTexturePath(0, gender, baseBody, race, 0).ToLowerInvariant().Replace("\\", "/");
            if (!string.IsNullOrEmpty(basePath) && files.TryGetValue(basePath, out string baseMatch))
            {
                if (baseMatch.Contains("do_not_edit") || baseMatch.Contains("_generated")) return overrideField;
                string fullPath = Path.Combine(modDir, subDir, baseMatch.Replace("/", "\\"));
                if (File.Exists(fullPath))
                {
                    plugin.Chat.Print($"[Drag And Drop Debug] Found override base for BodyType {baseBody}: {basePath}");
                    // Found base, now try to find normal
                    string normPath = RacePaths.GetBodyTexturePath(1, gender, baseBody, race, 0).ToLowerInvariant().Replace("\\", "/");
                    string fullNormPath = "";
                    if (!string.IsNullOrEmpty(normPath) && files.TryGetValue(normPath, out string normMatch))
                    {
                        fullNormPath = Path.Combine(modDir, subDir, normMatch.Replace("/", "\\"));
                    }

                    return new BackupTexturePaths(fullPath, fullNormPath);
                }
            }
            return overrideField;
        }

        public static int DetectRedirectedRace(Guid collectionId, int gender, int mainRace, DragAndDropTexturing.Plugin plugin = null)
        {
            return DetectRedirectedRace(GetActiveMods(collectionId), gender, mainRace, plugin);
        }

        public static int DetectRedirectedRace(List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods, int gender, int mainRace, DragAndDropTexturing.Plugin plugin = null)
        {
            string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
            if (plugin != null) plugin.Chat.Print($"[Drag And Drop Debug] Detecting redirection for Race {mainRace} (Gender: {gender})...");

            // Try to find a redirection for any body type
            for (int bodyType = 0; bodyType <= 3; bodyType++)
            {
                string nativePath = RacePaths.GetBodyTexturePath(0, gender, bodyType, mainRace, 0).ToLowerInvariant().Replace("\\", "/");
                if (plugin != null) plugin.Chat.Print($"[Drag And Drop Debug] Checking native path: {nativePath}");

                foreach (var mod in activeMods)
                {
                    var files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);
                    if (plugin != null) plugin.Chat.Print($"[Drag And Drop Debug] Mod '{mod.Name}' has {files.Count} files.");
                    if (files.TryGetValue(nativePath, out string match))
                    {
                        if (plugin != null) plugin.Chat.Print($"[Drag And Drop Debug] Match found in mod '{mod.Name}': {match}");
                        int redirected = RaceInfo.ReverseRaceLookup(match);
                        if (redirected != -1 && redirected != mainRace)
                        {
                            if (plugin != null) plugin.Chat.Print($"[Drag And Drop Debug] REDIRECTED to Race {redirected} ({RaceInfo.Races[redirected]})");
                            return redirected;
                        }
                    }
                }
            }
            if (plugin != null) plugin.Chat.Print("[Drag And Drop Debug] No redirection detected.");
            return -1;
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
                    if (modData?.Files != null)
                    {
                        foreach (var kvp in modData.Files)
                            files[kvp.Key.ToLowerInvariant().Replace("\\", "/")] = kvp.Value;
                    }
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
                                    foreach (var kvp in option.Files)
                                        files[kvp.Key.ToLowerInvariant().Replace("\\", "/")] = kvp.Value;
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
