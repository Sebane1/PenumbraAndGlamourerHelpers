using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using DragAndDropTexturing.Overlays;
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
using FFXIVLooseTextureCompiler;
using FFXIVLooseTextureCompiler.Export;
using FFXIVLooseTextureCompiler.Racial;
using LooseTextureCompilerCore;

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

            int totalChecked = 0, inheritedCount = 0, directCount = 0;
            foreach (var mod in mods)
            {
                string lowerKey = mod.Key.ToLower();
                string lowerValue = mod.Value.ToLower();
                if (lowerValue.Contains("drag and drop") || lowerKey.Contains("drag and drop") || lowerValue.Contains("loosetexturecompilerdlc") || lowerKey.Contains("loosetexturecompilerdlc")) continue;

                totalChecked++;
                var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                if (settings.Item1 == Penumbra.Api.Enums.PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1 == true)
                {
                    // Also check non-inherited to distinguish direct vs. inherited mods
                    var directSettings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, false);
                    bool isDirect = directSettings.Item1 == Penumbra.Api.Enums.PenumbraApiEc.Success && directSettings.Item2.HasValue && directSettings.Item2.Value.Item1 == true;
                    if (isDirect) directCount++;
                    else inheritedCount++;

                    activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                }
            }

            activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            DragAndDropTexturing.Plugin.Log?.Information($"[Drag And Drop Debug] GetActiveMods: Collection={collectionId}, {totalChecked} mods checked, {directCount} direct, {inheritedCount} inherited, {activeMods.Count} active total");
            return activeMods;
        }

        public static int DetectBaseBodyFromPenumbra(Guid collectionId, int gender, out string detectedModName, DragAndDropTexturing.Plugin plugin = null)
        {
            detectedModName = "";
            try
            {
                var activeMods = GetActiveMods(collectionId);
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] DetectBaseBody: {activeMods.Count} active mods found.");

                foreach (var mod in activeMods)
                {
                    // Skip our own generated mods
                    string lnm = mod.Name.ToLower();
                    string ldr = mod.Dir.ToLower();
                    if (lnm.Contains("texture body") || lnm.Contains("texture face") || lnm.Contains("texture eyes") || lnm.Contains("texture eyebrows") || lnm.Contains("texture gear") || lnm.Contains("texture mod") || ldr.Contains("do_not_edit") || ldr.Contains("texture gear"))
                    {
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Skipping own mod: '{mod.Name}'");
                        continue;
                    }

                    Dictionary<string, string> files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);

                    // Early hard-match: if the mod name or dir explicitly identifies a Gen3 variant body, return immediately.
                    // These body mods use standard chara/equipment paths so file-path counting alone won't detect them.
                    if (gender != 0)
                    {
                        if (lnm.Contains("pythia") || lnm.Contains("exqb") || lnm.Contains("gaia") || ldr.Contains("pythia") || ldr.Contains("exqb") || ldr.Contains("gaia"))
                        {
                            detectedModName = mod.Name;
                            plugin?.PluginLog?.Information($"[Drag And Drop Debug] Gen3 variant hard-detected via mod name: '{mod.Name}'");
                            return 2;
                        }
                    }

                    // Count paths per body type â?" the dominant type has more paths
                    int biboCount = 0, gen3Count = 0, tbseCount = 0;
                    foreach (var key in files.Keys)
                    {
                        string lowerKey = key.ToLowerInvariant();
                        if (lowerKey.StartsWith("chara/bibo_")) biboCount++;
                        if (lowerKey.Contains("tfgen3")) gen3Count++;
                        if (lowerKey.Contains("_b_d") && lowerKey.Contains("obj/body")) tbseCount++;
                    }

                    if (gender == 1) tbseCount = 0; // TBSE is not for females
                    if (gender == 0) { biboCount = 0; gen3Count = 0; } // Bibo/Gen3 is not for males

                    int bodyTypeCount = (biboCount > 0 ? 1 : 0) + (gen3Count > 0 ? 1 : 0) + (tbseCount > 0 ? 1 : 0);

                    if (bodyTypeCount == 1)
                    {
                        detectedModName = mod.Name;
                        int result = biboCount > 0 ? 1 : gen3Count > 0 ? 2 : 3;
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via paths: '{mod.Name}' -> type {result}");
                        return result;
                    }
                    else if (bodyTypeCount > 1)
                    {
                        // Multiple body types â?" check name first, then use whichever has more paths
                        detectedModName = mod.Name;
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Multiple body types in '{mod.Name}' (bibo={biboCount}, gen3={gen3Count}, tbse={tbseCount}). Checking name...");
                        if (gender != 0) { if (lnm.Contains("bibo") || lnm.Contains("yab") || lnm.Contains("b+") || ldr.Contains("bibo")) return 1; if (lnm.Contains("gen3") || lnm.Contains("tight & firm") || System.Text.RegularExpressions.Regex.IsMatch(lnm, @"(^|[^a-z])eve([^a-z]|$)") || lnm.Contains("exqb") || lnm.Contains("pythia") || lnm.Contains("gaia") || ldr.Contains("gen3") || ldr.Contains("exqb") || ldr.Contains("pythia") || ldr.Contains("gaia")) return 2; } if (gender != 1) { if (lnm.Contains("tbse") || ldr.Contains("tbse")) return 3; }
                        // Name didn't help â?" the type with the most paths is the primary one
                        int max = Math.Max(biboCount, Math.Max(gen3Count, tbseCount));
                        int dominant = gen3Count == max ? 2 : biboCount == max ? 1 : 3;
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Name inconclusive. Dominant path count wins: type {dominant}");
                        return dominant;
                    }
                    else
                    {
                        // No body paths found â?" check mod name/dir as last resort
                        if (gender != 0) {
                            if (lnm.Contains("bibo") || lnm.Contains("yab") || lnm.Contains("b+") || ldr.Contains("bibo")) { detectedModName = mod.Name; plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> Bibo"); return 1; }
                            if (lnm.Contains("gen3") || lnm.Contains("tight & firm") || System.Text.RegularExpressions.Regex.IsMatch(lnm, @"(^|[^a-z])eve([^a-z]|$)") || lnm.Contains("exqb") || lnm.Contains("pythia") || lnm.Contains("gaia") || ldr.Contains("gen3") || ldr.Contains("exqb") || ldr.Contains("pythia") || ldr.Contains("gaia")) { detectedModName = mod.Name; plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> Gen3"); return 2; }
                        }
                        if (gender != 1) {
                            if (lnm.Contains("tbse") || ldr.Contains("tbse")) { detectedModName = mod.Name; plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> TBSE"); return 3; }
                        }
                    }
                }
            }
            catch (Exception ex) { Log?.Warning(ex, "Failed to detect base body from Penumbra"); }
            
            if (plugin != null && plugin.Configuration.FallbackBodyType > 0)
            {
                int fallback = plugin.Configuration.FallbackBodyType - 1;
                if (fallback == 4) fallback = 5; // Otopop is 5
                
                detectedModName = "Manual Fallback";
                plugin.PluginLog.Information($"[Drag And Drop Debug] DetectBaseBody: No body mod found, using Manual Fallback BodyType: {fallback}");
                return fallback;
            }

            plugin?.PluginLog?.Information("[Drag And Drop Debug] DetectBaseBody: No body mod found!");
            return -1;
        }

        public static void ExtractActiveTextureFromPenumbra(Guid collectionId, string category, string raceCode, string subRaceName, out string extractedModName, out string extractedBase, out string extractedNormal, out string extractedMask, DragAndDropTexturing.Plugin plugin, FFXIVLooseTextureCompiler.PathOrganization.TextureSet item = null)
        {
            extractedModName = ""; extractedBase = ""; extractedNormal = ""; extractedMask = "";
            if (item == null) return;

            try
            {
                plugin.PluginLog.Information($"[Drag And Drop Debug] Searching for {category} texture. RaceCode: {raceCode}, InternalBase: {item.InternalBasePath}");

                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] Total mods in list: {mods.Count}");

                List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)>();
                foreach (var mod in mods)
                {
                    // SKIP OUR OWN MODS â?" our generated mods always end with "Texture Body/Face/Eyes/Eyebrows"
                    string modNameLower = mod.Value.ToLower();
                    string modDirLower = mod.Key.ToLower();
                    if (modNameLower.Contains("drag and drop") || modDirLower.Contains("drag and drop") ||
                        modNameLower.Contains("texture body") || modNameLower.Contains("texture face") || modNameLower.Contains("texture eyes") ||
                        modNameLower.Contains("texture eyebrows") || modNameLower.Contains("texture gear") || modNameLower.Contains("texture mod") ||
                        modDirLower.Contains("texture gear")) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1)
                    {
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                    }
                }
                activeMods.Sort((a, b) => 
                {
                    int result = b.Priority.CompareTo(a.Priority);
                    if (result == 0) result = a.Name.CompareTo(b.Name);
                    return result;
                });
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] Active mods (non-self): {activeMods.Count}");

                foreach (var mod in activeMods)
                {
                    Dictionary<string, string> files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);

                    // Try to find the paths from the item in this mod (keys are lowercase in the dictionary)
                    string baseLookup = item.InternalBasePath?.ToLowerInvariant().Replace("\\", "/") ?? "";
                    string normLookup = item.InternalNormalPath?.ToLowerInvariant().Replace("\\", "/") ?? "";
                    string maskLookup = item.InternalMaskPath?.ToLowerInvariant().Replace("\\", "/") ?? "";
                    if (!string.IsNullOrEmpty(baseLookup) && files.TryGetValue(baseLookup, out string baseMatch))
                    {


                        string fullPath = Path.Combine(modDirectoryPath, mod.Dir, baseMatch.Replace("/", "\\"));
                        if (File.Exists(fullPath))
                        {
                            extractedBase = fullPath;
                            extractedModName = mod.Name;

                            // Get Normal and Mask from SAME mod if they exist
                            if (!string.IsNullOrEmpty(normLookup) && files.TryGetValue(normLookup, out string normMatch))
                                extractedNormal = Path.Combine(modDirectoryPath, mod.Dir, normMatch.Replace("/", "\\"));

                            if (!string.IsNullOrEmpty(maskLookup) && files.TryGetValue(maskLookup, out string maskMatch))
                                extractedMask = Path.Combine(modDirectoryPath, mod.Dir, maskMatch.Replace("/", "\\"));

                            plugin.PluginLog.Information($"[Drag And Drop Debug] Found base: {item.InternalBasePath} in mod {mod.Name}");
                            return;
                        }
                        else
                        {
                            plugin.PluginLog.Information($"[Drag And Drop Debug] Path matched in {mod.Name} but file not found: {fullPath}");
                        }
                    }
                }

                plugin.PluginLog.Information($"[Drag And Drop Debug] No modded {category} texture found for path: {item.InternalBasePath}");
            }
            catch (Exception ex)
            {
                plugin.PluginLog.Information($"[Drag And Drop Debug] ERROR in ExtractActiveTextureFromPenumbra: {ex.Message}");
            }
        }

        private static void CollectAdvancedOverlaysFromMod(string modRoot, string modName,
            Dictionary<string, List<string>> modSettings, string collectionIdString, DragAndDropTexturing.Plugin plugin)
        {
            string metadataPath = AdvancedOverlayParser.FindMetadataJsonPath(modRoot);
            if (metadataPath == null)
                return;

            plugin?.PluginLog?.Information($"[Drag And Drop Debug] Found overlay metadata at: {metadataPath}");
            var advancedMod = AdvancedOverlayParser.Parse(metadataPath);
            if (advancedMod == null)
                return;

            string sidecarRoot = Path.GetDirectoryName(metadataPath);
            var coverageMaskPaths = ResolveProteusCoverageMaskPaths(modRoot, sidecarRoot, modSettings);

            if (advancedMod.Overlays != null)
            {
                AddResolvedOverlays(advancedMod.Overlays, sidecarRoot, modName, collectionIdString, plugin,
                    "(unconditional)", advancedMod.ColorTableRows, coverageMaskPaths);
            }

            if (advancedMod.OptionGroups == null)
                return;

            foreach (var group in advancedMod.OptionGroups)
            {
                if (group.PenumbraGroupName != null
                    && group.PenumbraGroupName.Equals("Masks", StringComparison.OrdinalIgnoreCase))
                    continue;
                var activeOptionsPair = modSettings.FirstOrDefault(s =>
                    s.Key.Equals(group.PenumbraGroupName, StringComparison.OrdinalIgnoreCase));
                var activeOptions = activeOptionsPair.Value;

                if (activeOptions == null)
                    activeOptions = BuildDefaultPenumbraGroupSelection(modRoot, group.PenumbraGroupName, group.Options?.Count ?? 0);

                plugin?.PluginLog?.Information(
                    $"[Drag And Drop Debug] Group: '{group.PenumbraGroupName}'. Active options: {activeOptions?.Count ?? 0}");

                if (activeOptions == null || group.Options == null)
                    continue;

                for (int optionIndex = 0; optionIndex < group.Options.Count; optionIndex++)
                {
                    var option = group.Options[optionIndex];
                    bool isSelected = AdvancedOverlayParser.IsOptionSelected(option.Name, optionIndex, activeOptions);
                    plugin?.PluginLog?.Information(
                        $"[Drag And Drop Debug] - Option '{option.Name}': selected={isSelected}");

                    if (isSelected && option.Overlays != null)
                    {
                        var colorRows = option.ColorTableRows ?? advancedMod.ColorTableRows;
                        AddResolvedOverlays(option.Overlays, sidecarRoot, modName, collectionIdString, plugin,
                            option.Name, colorRows, coverageMaskPaths);
                    }
                }
            }
        }

        private static List<string> BuildDefaultPenumbraGroupSelection(string modRoot, string groupName, int optionCount)
        {
            long defaultSettings = 0;
            string groupType = "Single";
            int resolvedOptionCount = optionCount;

            if (TryGetPenumbraGroupDefaults(modRoot, groupName, out var metaDefaults, out var metaType, out var metaOptionCount))
            {
                defaultSettings = metaDefaults;
                groupType = metaType ?? groupType;
                if (metaOptionCount > 0)
                    resolvedOptionCount = metaOptionCount;
            }
            else
            {
                try
                {
                    if (Directory.Exists(modRoot))
                    {
                        foreach (var groupFile in Directory.GetFiles(modRoot, "group_*.json"))
                        {
                            var groupData = JsonConvert.DeserializeObject<PenumbraGroupData>(File.ReadAllText(groupFile));
                            if (groupData != null && groupData.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                            {
                                defaultSettings = groupData.DefaultSettings;
                                groupType = groupData.Type ?? groupType;
                                resolvedOptionCount = groupData.Options?.Count ?? resolvedOptionCount;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            var activeOptions = new List<string>();
            if (groupType.Equals("Multi", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < resolvedOptionCount; i++)
                {
                    if ((defaultSettings & (1L << i)) != 0)
                        activeOptions.Add(i.ToString());
                }
            }
            else
            {
                activeOptions.Add(defaultSettings.ToString());
            }

            return activeOptions;
        }

        private static bool TryGetPenumbraGroupDefaults(string modRoot, string groupName,
            out long defaultSettings, out string groupType, out int optionCount)
        {
            defaultSettings = 0;
            groupType = "Single";
            optionCount = 0;

            string metaJsonPath = Path.Combine(modRoot, "meta.json");
            if (!File.Exists(metaJsonPath))
                return false;

            try
            {
                string rawJson = File.ReadAllText(metaJsonPath);
                if (!rawJson.Contains("\"Groups\""))
                    return false;

                var meta = JsonConvert.DeserializeObject<PenumbraModMetaV4>(rawJson);
                var group = meta?.Groups?.FirstOrDefault(g =>
                    g.Name != null && g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                    return false;

                defaultSettings = group.DefaultSettings;
                groupType = group.Type ?? "Single";
                optionCount = group.Options?.Count ?? 0;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> ResolveProteusCoverageMaskPaths(string modRoot, string sidecarRoot,
            Dictionary<string, List<string>> modSettings)
        {
            const string maskGroupName = "Masks";
            var result = new List<string>();
            var activeOptionsPair = modSettings.FirstOrDefault(s =>
                s.Key.Equals(maskGroupName, StringComparison.OrdinalIgnoreCase));
            var activeOptions = activeOptionsPair.Value;
            if (activeOptions == null)
                activeOptions = BuildDefaultPenumbraGroupSelection(modRoot, maskGroupName, 0);

            if (activeOptions == null || activeOptions.Count == 0)
                return result;

            try
            {
                string masksDir = Path.Combine(sidecarRoot, "Masks");
                if (!Directory.Exists(masksDir))
                    return result;

                var groupOptions = GetPenumbraGroupOptionNames(modRoot, maskGroupName);
                foreach (var active in activeOptions)
                {
                    string trimmed = active.Trim();
                    string maskName = trimmed;
                    if (int.TryParse(trimmed, out int idx) && idx >= 0 && idx < groupOptions.Count)
                        maskName = groupOptions[idx];

                    if (string.IsNullOrEmpty(maskName) || maskName.Equals("None", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string maskFile = Path.Combine(masksDir, maskName + ".png");
                    if (File.Exists(maskFile))
                        result.Add(maskFile);
                }
            }
            catch { }

            return result;
        }

        private static List<string> GetPenumbraGroupOptionNames(string modRoot, string groupName)
        {
            var names = new List<string>();
            string metaJsonPath = Path.Combine(modRoot, "meta.json");
            if (File.Exists(metaJsonPath))
            {
                try
                {
                    var meta = JsonConvert.DeserializeObject<PenumbraModMetaV4>(File.ReadAllText(metaJsonPath));
                    var group = meta?.Groups?.FirstOrDefault(g =>
                        g.Name != null && g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                    if (group?.Options != null)
                    {
                        foreach (var opt in group.Options)
                            names.Add(opt.Name);
                        return names;
                    }
                }
                catch { }
            }

            try
            {
                foreach (var groupFile in Directory.GetFiles(modRoot, "group_*.json"))
                {
                    var groupData = JsonConvert.DeserializeObject<PenumbraGroupData>(File.ReadAllText(groupFile));
                    if (groupData != null && groupData.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)
                        && groupData.Options != null)
                    {
                        foreach (var opt in groupData.Options)
                            names.Add(opt.Name);
                        break;
                    }
                }
            }
            catch { }

            return names;
        }

        private static void AddResolvedOverlays(IEnumerable<AdvancedOverlay> overlays, string sidecarRoot,
            string modName, string collectionIdString, DragAndDropTexturing.Plugin plugin, string contextLabel,
            List<AdvancedColorTableRow> colorTableRows, List<string> coverageMaskPaths)
        {
            foreach (var overlay in overlays)
            {
                var resolved = AdvancedOverlayParser.CreateResolved(modName, overlay, sidecarRoot, colorTableRows, coverageMaskPaths);
                if (resolved == null)
                    continue;

                if (resolved.RequiresShaderPath)
                {
                    plugin?.PluginLog?.Information(
                        $"[Drag And Drop Debug]   -> Skipping shader-only overlay ({contextLabel}) from '{modName}'");
                    continue;
                }

                plugin?.PluginLog?.Information(
                    $"[Drag And Drop Debug]   -> Overlay ({contextLabel}): diff={resolved.DiffusePath != null}, norm={resolved.NormalPath != null}, mask={resolved.MaskPath != null}, index={resolved.IndexPath != null}, colorRows={resolved.ColorTableRows?.Count ?? 0}, masks={resolved.CoverageMaskPaths?.Count ?? 0}");

                if (!AdvancedOverlayParser.ActiveOverlays.ContainsKey(collectionIdString))
                    AdvancedOverlayParser.ActiveOverlays[collectionIdString] = new List<ResolvedAdvancedOverlay>();

                AdvancedOverlayParser.ActiveOverlays[collectionIdString].Add(resolved);
            }
        }

        private static void AddResolvedOverlays(IEnumerable<AdvancedOverlay> overlays, string sidecarRoot,
            string modName, string collectionIdString, DragAndDropTexturing.Plugin plugin, string contextLabel)
        {
            AddResolvedOverlays(overlays, sidecarRoot, modName, collectionIdString, plugin, contextLabel, null, null);
        }

        public static void PopulateOmniOverrides(Guid collectionId, int gender, int race, DragAndDropTexturing.Plugin plugin)
        {
            var collectionIdString = collectionId.ToString();
            // Clear prior static values to prevent stale reads
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.BiboOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.Gen3Override = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.Gen2Override = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.TbseOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.OtopopOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.VanillaLalaOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.RelalaOverride = null;

            AdvancedOverlayParser.ActiveOverlays.Clear();

            try
            {
                plugin?.PluginLog?.Information("[Drag And Drop Debug] Populating Omni Overrides...");
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();

                List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)>();
                foreach (var mod in mods)
                {
                    string modNameLower = mod.Value.ToLower();
                    string modDirLower = mod.Key.ToLower();
                    if (modNameLower.Contains("drag and drop") || modDirLower.Contains("drag and drop") ||
                        modNameLower.Contains("texture body") || modNameLower.Contains("texture face") || modNameLower.Contains("texture eyes") ||
                        modNameLower.Contains("texture eyebrows") || modNameLower.Contains("texture gear") || modNameLower.Contains("texture mod") ||
                        modDirLower.Contains("texture gear")) continue;
                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1)
                    {
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                    }
                }
                activeMods.Sort((a, b) => 
                {
                    int result = b.Priority.CompareTo(a.Priority);
                    if (result == 0) result = a.Name.CompareTo(b.Name);
                    return result;
                });

                // Detect if character is redirected to another race (e.g. Hroth to Midlander)
                int mainRace = RaceInfo.SubRaceToMainRace(race);

                foreach (var mod in activeMods)
                {
                    Dictionary<string, string> files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);

                    // Diagnostic: show what body types this mod contains
                    int biboKeys = 0, gen3Keys = 0, vanillaKeys = 0;
                    foreach (var key in files.Keys)
                    {
                        if (key.StartsWith("chara/bibo_")) biboKeys++;
                        if (key.Contains("tfgen3")) gen3Keys++;
                        if (key.Contains("obj/body") && !key.Contains("tfgen3") && !key.StartsWith("chara/bibo_")) vanillaKeys++;
                    }
                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Checking mod '{mod.Name}' (Priority: {mod.Priority}, Files: {files.Count}, Bibo: {biboKeys}, Gen3: {gen3Keys}, Vanilla: {vanillaKeys}) for Race {mainRace}...");

                    if (BackupTexturePaths.OverrideMode)
                    {
                        // Check Bibo+ (baseBody 1)
                        BackupTexturePaths.BiboOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 1, gender, mainRace, BackupTexturePaths.BiboOverride, plugin);
                        // Check Gen3 (baseBody 2)
                        BackupTexturePaths.Gen3Override = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 2, gender, mainRace, BackupTexturePaths.Gen3Override, plugin);
                        // Check Vanilla/Gen2 (baseBody 0)
                        BackupTexturePaths.Gen2Override = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 0, gender, mainRace, BackupTexturePaths.Gen2Override, plugin);
                        // Check TBSE (baseBody 3)
                        BackupTexturePaths.TbseOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 3, gender, mainRace, BackupTexturePaths.TbseOverride, plugin);
                        // Check Otopop (baseBody 5)
                        BackupTexturePaths.OtopopOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 5, gender, mainRace, BackupTexturePaths.OtopopOverride, plugin);
                        // Check Asym Lalafell (baseBody 6)
                        BackupTexturePaths.VanillaLalaOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 6, gender, mainRace, BackupTexturePaths.VanillaLalaOverride, plugin);
                        // Check Relala (baseBody 7)
                        BackupTexturePaths.RelalaOverride = CheckAndSetOverride(files, modDirectoryPath, mod.Dir, mod.Name, 7, gender, mainRace, BackupTexturePaths.RelalaOverride, plugin);
                    }

                    // Scan for Proteus / advanced overlay sidecars
                    string modRoot = Path.Combine(modDirectoryPath, mod.Dir);
                    CollectAdvancedOverlaysFromMod(modRoot, mod.Name, mod.Settings, collectionIdString, plugin);

                    if (BackupTexturePaths.OverrideMode)
                    {
                        // After each mod, cross-convert any gaps between Bibo+/Gen3 immediately
                        // so lower-priority mods can't fill the slot with their own textures
                        CrossConvertMissingOverrides(plugin);
                    }
                }
                plugin?.PluginLog?.Information("[Drag And Drop Debug] Omni Overrides population check complete.");
            }
            catch (Exception ex)
            {
                plugin.PluginLog.Information($"[Drag And Drop Debug] ERROR in PopulateOmniOverrides: {ex.Message}");
            }
        }

        /// <summary>
        /// After PopulateOmniOverrides scans all mods, this detects gaps where a high-priority skin mod
        /// provided one body type (e.g. Bibo+) but not another (e.g. Gen3). It auto-converts using
        /// FastUVTransfer so the high-priority mod's textures take precedence over lower-priority mods
        /// that might have native textures for the missing body type.
        /// </summary>
        private static void CrossConvertMissingOverrides(DragAndDropTexturing.Plugin plugin)
        {
            try
            {
                bool hasBibo = BackupTexturePaths.BiboOverride != null && !string.IsNullOrEmpty(BackupTexturePaths.BiboOverride.ModName);
                bool hasGen3 = BackupTexturePaths.Gen3Override != null && !string.IsNullOrEmpty(BackupTexturePaths.Gen3Override.ModName);

                plugin?.PluginLog?.Information($"[Drag And Drop Debug] CrossConvert check: hasBibo={hasBibo} ('{BackupTexturePaths.BiboOverride?.ModName}'), hasGen3={hasGen3} ('{BackupTexturePaths.Gen3Override?.ModName}')");

                // Nothing to cross-convert if neither or both exist
                if (hasBibo == hasGen3) return;

                string crossConvertDir = Path.Combine(
                    GlobalPathStorage.OriginalBaseDirectory ??
                    Path.Combine(PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke(), "LooseTextureCompilerDLC"),
                    "cross_convert_cache");
                Directory.CreateDirectory(crossConvertDir);

                if (hasBibo && !hasGen3)
                {
                    // Bibo+ exists, Gen3 is missing â+' convert Bibo+ to Gen3
                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert: Bibo+ '{BackupTexturePaths.BiboOverride.ModName}' â+' Gen3 (auto-generated)");

                    string outBase = "";
                    if (!string.IsNullOrEmpty(BackupTexturePaths.BiboOverride.Base) && File.Exists(BackupTexturePaths.BiboOverride.Base))
                    {
                        var baseHash = TextureProcessor.CreateHashLocal(BackupTexturePaths.BiboOverride.Base);
                        outBase = Path.Combine(crossConvertDir, BackupTexturePaths.BiboOverride.ModName + $" {baseHash} " + Path.GetFileName(BackupTexturePaths.BiboOverride.Base).Replace(".tex", ".png"));
                        if (!File.Exists(outBase))
                        {
                            FastUVTransfer.BiboToGen3(BackupTexturePaths.BiboOverride.Base, outBase);
                        }
                    }

                    string outNorm = "";
                    if (!string.IsNullOrEmpty(BackupTexturePaths.BiboOverride.Normal) && File.Exists(BackupTexturePaths.BiboOverride.Normal))
                    {
                        var normHash = TextureProcessor.CreateHashLocal(BackupTexturePaths.BiboOverride.Normal);
                        outNorm = Path.Combine(crossConvertDir, BackupTexturePaths.BiboOverride.ModName + $" {normHash} " + Path.GetFileName(BackupTexturePaths.BiboOverride.Normal).Replace(".tex", ".png"));
                        if (!File.Exists(outNorm))
                        {
                            FastUVTransfer.BiboToGen3(BackupTexturePaths.BiboOverride.Normal, outNorm);
                        }
                    }


                    string outMask = "";
                    if (!string.IsNullOrEmpty(BackupTexturePaths.BiboOverride.Mask) && File.Exists(BackupTexturePaths.BiboOverride.Mask))
                    {
                        var maskHash = TextureProcessor.CreateHashLocal(BackupTexturePaths.BiboOverride.Mask);
                        if (!File.Exists(outMask))
                        {
                            outMask = Path.Combine(crossConvertDir, BackupTexturePaths.BiboOverride.ModName + $" {maskHash} " + Path.GetFileName(BackupTexturePaths.BiboOverride.Mask).Replace(".tex", ".png"));
                            FastUVTransfer.BiboToGen3(BackupTexturePaths.BiboOverride.Mask, outMask);
                        }
                    }

                    var btp = new BackupTexturePaths(outBase, outNorm, outMask);
                    btp.FillsMissingTextures = true;
                    btp.ModName = BackupTexturePaths.BiboOverride.ModName + " (Auto-Converted Gen3)";
                    BackupTexturePaths.Gen3Override = btp;

                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert complete: Gen3 override set from Bibo+ source.");
                }
                else if (hasGen3 && !hasBibo)
                {
                    // Gen3 exists, Bibo+ is missing â+' convert Gen3 to Bibo+
                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert: Gen3 '{BackupTexturePaths.Gen3Override.ModName}' â+' Bibo+ (auto-generated)");

                    string outBase = "";
                    if (!string.IsNullOrEmpty(BackupTexturePaths.Gen3Override.Base) && File.Exists(BackupTexturePaths.Gen3Override.Base))
                    {
                        var baseHash = TextureProcessor.CreateHashLocal(BackupTexturePaths.BiboOverride.Base);
                        outBase = Path.Combine(crossConvertDir, BackupTexturePaths.Gen3Override.ModName + $" {baseHash} " + Path.GetFileName(BackupTexturePaths.Gen3Override.Base).Replace(".tex", ".png"));
                        if (!File.Exists(outBase))
                        {
                            FastUVTransfer.Gen3ToBibo(BackupTexturePaths.Gen3Override.Base, outBase);
                        }
                    }

                    string outNorm = "";
                    if (!string.IsNullOrEmpty(BackupTexturePaths.Gen3Override.Normal) && File.Exists(BackupTexturePaths.Gen3Override.Normal))
                    {
                        var normHash = TextureProcessor.CreateHashLocal(BackupTexturePaths.BiboOverride.Base);
                        outNorm = Path.Combine(crossConvertDir, BackupTexturePaths.Gen3Override.ModName + $" {normHash} " + Path.GetFileName(BackupTexturePaths.Gen3Override.Normal).Replace(".tex", ".png"));
                        if (!File.Exists(outNorm))
                        {
                            FastUVTransfer.Gen3ToBibo(BackupTexturePaths.Gen3Override.Normal, outNorm);
                        }
                    }

                    string outMask = "";
                    if (!string.IsNullOrEmpty(BackupTexturePaths.Gen3Override.Mask) && File.Exists(BackupTexturePaths.Gen3Override.Mask))
                    {
                        var maskHash = TextureProcessor.CreateHashLocal(BackupTexturePaths.BiboOverride.Base);
                        if (!File.Exists(outMask))
                        {
                            outMask = Path.Combine(crossConvertDir, BackupTexturePaths.Gen3Override.ModName + $" {maskHash} " + Path.GetFileName(BackupTexturePaths.Gen3Override.Mask).Replace(".tex", ".png"));
                            if (!File.Exists(outMask))
                            {
                                FastUVTransfer.Gen3ToBibo(BackupTexturePaths.Gen3Override.Mask, outMask);
                            }
                        }
                    }

                    var btp = new BackupTexturePaths(outBase, outNorm, outMask);
                    btp.ModName = BackupTexturePaths.Gen3Override.ModName + " (Auto-Converted Bibo+)";
                    btp.FillsMissingTextures = true;
                    BackupTexturePaths.BiboOverride = btp;

                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert complete: Bibo+ override set from Gen3 source.");
                }
            }
            catch (Exception ex)
            {
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] ERROR in CrossConvertMissingOverrides: {ex.Message}");
            }
        }

        private static BackupTexturePaths CheckAndSetOverride(Dictionary<string, string> files, string modDir, string subDir, string modName, int baseBody, int gender, int race, BackupTexturePaths overrideField, DragAndDropTexturing.Plugin plugin)
        {
            // If already fully populated (base + normal + mask all set), nothing to do
            if (overrideField != null && !overrideField.NeedsNormal && !overrideField.NeedsMask)
                return overrideField;

            // Helper: try to find a texture match in this mod for a given texture type
            string FindTexture(int textureType, string suffix, int overrideBaseBody = -1)
            {
                int targetBaseBody = overrideBaseBody == -1 ? baseBody : overrideBaseBody;
                string texPath = RacePaths.GetBodyTexturePath(textureType, gender, targetBaseBody, race, 0).ToLowerInvariant().Replace("\\", "/");
                string texPathV01 = !string.IsNullOrEmpty(texPath) ? texPath.Replace("/texture/c", "/texture/v01_c").Replace("/texture/tfgen3", "/texture/v01_tfgen3") : "";

                string match = null;
                if (!string.IsNullOrEmpty(texPath) && files.TryGetValue(texPath, out string v1)) match = v1;
                else if (!string.IsNullOrEmpty(texPathV01) && files.TryGetValue(texPathV01, out string v2)) match = v2;

                // Broader fallback (race-aware)
                if (match == null)
                {
                    string bodyMarker = null;
                    string raceId = null;
                    switch (targetBaseBody)
                    {
                        case 1: // Bibo+
                            bodyMarker = "chara/bibo_";
                            if (!string.IsNullOrEmpty(texPath) && texPath.StartsWith("chara/bibo_"))
                            {
                                string afterMarker = texPath.Substring(11);
                                int endIdx = afterMarker.IndexOf('_');
                                if (endIdx > 0) raceId = afterMarker.Substring(0, endIdx);
                            }
                            break;
                        case 2: // Gen3
                            bodyMarker = "tfgen3";
                            if (!string.IsNullOrEmpty(texPath))
                            {
                                int tfIdx = texPath.IndexOf("tfgen3");
                                if (tfIdx >= 0)
                                {
                                    string afterGen3 = texPath.Substring(tfIdx + 6);
                                    int endIdx = afterGen3.IndexOf('f');
                                    if (endIdx > 0) raceId = afterGen3.Substring(0, endIdx);
                                }
                            }
                            break;
                    }

                    if (bodyMarker != null && !string.IsNullOrEmpty(raceId) && raceId != "invalid")
                    {
                        foreach (var key in files.Keys)
                        {
                            if (key.Contains(bodyMarker) && key.Contains(raceId) && key.EndsWith(suffix))
                            {
                                match = files[key];
                                break;
                            }
                        }
                    }
                }

                if (match == null) return null;
                string fullPath = Path.Combine(modDir, subDir, match.Replace("/", "\\"));
                return File.Exists(fullPath) ? fullPath : null;
            }

            // Case 1: Override already exists but has gaps in normal/mask
            if (overrideField != null)
            {
                bool filled = false;
                if (overrideField.NeedsNormal)
                {
                    string normFullPath = FindTexture(1, "_norm.tex") ?? FindTexture(1, "_n.tex") ?? FindTexture(1, "_norm.tex", 0) ?? FindTexture(1, "_n.tex", 0);
                    if (normFullPath != null)
                    {
                        overrideField.FillNormal(normFullPath);
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Gap-filled normal for BodyType {baseBody} from '{modName}'");
                        filled = true;
                    }
                }
                if (overrideField.NeedsMask)
                {
                    string maskFullPath = FindTexture(2, "_mask.tex") ?? FindTexture(2, "_m.tex") ?? FindTexture(2, "_s.tex") ?? FindTexture(2, "_mask.tex", 0) ?? FindTexture(2, "_m.tex", 0) ?? FindTexture(2, "_s.tex", 0);
                    if (maskFullPath != null)
                    {
                        overrideField.FillMask(maskFullPath);
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Gap-filled mask for BodyType {baseBody} from '{modName}'");
                        filled = true;
                    }
                }
                return overrideField;
            }

            // Case 2: No override yet, try to claim the slot with a base texture
            string baseFullPath = FindTexture(0, "_base.tex") ?? FindTexture(0, "_d.tex") ?? FindTexture(0, "_diff.tex");

            if (baseFullPath == null)
            {
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] No match for BodyType {baseBody} in '{modName}'");
                return overrideField;
            }

            plugin?.PluginLog?.Information($"[Drag And Drop Debug] Found override base for BodyType {baseBody} in '{modName}': {baseFullPath}");

            string normPath = FindTexture(1, "_norm.tex") ?? FindTexture(1, "_n.tex") ?? FindTexture(1, "_norm.tex", 0) ?? FindTexture(1, "_n.tex", 0);
            string maskPath = FindTexture(2, "_mask.tex") ?? FindTexture(2, "_m.tex") ?? FindTexture(2, "_s.tex") ?? FindTexture(2, "_mask.tex", 0) ?? FindTexture(2, "_m.tex", 0) ?? FindTexture(2, "_s.tex", 0);

            var btp = new BackupTexturePaths(baseFullPath, normPath ?? "", maskPath ?? "");
            btp.ModName = modName;
            return btp;
        }

        public static int DetectRedirectedRace(Guid collectionId, int gender, int mainRace, DragAndDropTexturing.Plugin plugin = null)
        {
            return DetectRedirectedRace(GetActiveMods(collectionId), gender, mainRace, plugin);
        }

        public static int DetectRedirectedRace(List<(string Name, string Dir, int Priority, Dictionary<string, List<string>> Settings)> activeMods, int gender, int mainRace, DragAndDropTexturing.Plugin plugin = null)
        {
            string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
            if (plugin != null) plugin.PluginLog.Information($"[Drag And Drop Debug] Detecting redirection for Race {mainRace} (Gender: {gender})...");

            // Try to find a redirection for any body type
            for (int bodyType = 0; bodyType <= 3; bodyType++)
            {
                string nativePath = RacePaths.GetBodyTexturePath(0, gender, bodyType, mainRace, 0).ToLowerInvariant().Replace("\\", "/");
                if (plugin != null) plugin.PluginLog.Information($"[Drag And Drop Debug] Checking native path: {nativePath}");

                foreach (var mod in activeMods)
                {
                    var files = GetFilesForMod(modDirectoryPath, mod.Dir, mod.Settings);
                    if (plugin != null) plugin.PluginLog.Information($"[Drag And Drop Debug] Mod '{mod.Name}' has {files.Count} files.");
                    if (files.TryGetValue(nativePath, out string match))
                    {
                        if (plugin != null) plugin.PluginLog.Information($"[Drag And Drop Debug] Match found in mod '{mod.Name}': {match}");
                        int redirected = RaceInfo.ReverseRaceLookup(match);
                        if (redirected != -1 && redirected != mainRace)
                        {
                            if (plugin != null) plugin.PluginLog.Information($"[Drag And Drop Debug] REDIRECTED to Race {redirected} ({RaceInfo.Races[redirected]})");
                            return redirected;
                        }
                    }
                }
            }
            if (plugin != null) plugin.PluginLog.Information("[Drag And Drop Debug] No redirection detected.");
            return -1;
        }

        // Extensions we care about â€” skip everything else (audio, animation, VFX, etc.)
        private static readonly HashSet<string> _relevantExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mtrl", ".tex", ".mdl" };

        private static bool IsRelevantGamePath(string key)
        {
            int dotIdx = key.LastIndexOf('.');
            if (dotIdx < 0) return false;
            return _relevantExtensions.Contains(key.Substring(dotIdx));
        }

        private static Dictionary<string, string> GetFilesForMod(string modDirectory, string modDir, Dictionary<string, List<string>> settings)
        {
            Dictionary<string, string> files = new Dictionary<string, string>();
            string modPath = Path.Combine(modDirectory, modDir);

            // Check meta.json first
            bool handledByMeta = false;
            string metaJson = Path.Combine(modPath, "meta.json");
            if (File.Exists(metaJson))
            {
                try
                {
                    string rawJson = File.ReadAllText(metaJson);
                    if (rawJson.Contains("\"DefaultData\"") || rawJson.Contains("\"Groups\""))
                    {
                        handledByMeta = true;
                        if (rawJson.Contains(".scd") || rawJson.Contains(".pap") || rawJson.Contains(".vfx") || rawJson.Contains(".avfx") || rawJson.Contains(".tmb") || rawJson.Contains(".sklb") || rawJson.Contains(".shpk"))
                            return files;

                        var meta = JsonConvert.DeserializeObject<PenumbraModMetaV4>(rawJson);
                        if (meta?.DefaultData?.Files != null)
                        {
                            foreach (var kvp in meta.DefaultData.Files)
                            {
                                string normalizedKey = kvp.Key.ToLowerInvariant().Replace("\\", "/");
                                if (IsRelevantGamePath(normalizedKey))
                                    files[normalizedKey] = kvp.Value;
                            }
                        }

                        if (meta?.Groups != null)
                        {
                            foreach (var groupData in meta.Groups)
                            {
                                var activeOptionsPair = settings.FirstOrDefault(s => s.Key.Equals(groupData.Name, StringComparison.OrdinalIgnoreCase));
                                var activeOptions = activeOptionsPair.Value;
                                if (activeOptions == null)
                                {
                                    activeOptions = new List<string>();
                                    if (groupData.Type != null && groupData.Type.Equals("Multi", StringComparison.OrdinalIgnoreCase))
                                    {
                                        for (int i = 0; i < groupData.Options.Count; i++)
                                        {
                                            if ((groupData.DefaultSettings & (1L << i)) != 0)
                                            {
                                                activeOptions.Add(i.ToString());
                                            }
                                        }
                                    }
                                    else
                                    {
                                        activeOptions.Add(groupData.DefaultSettings.ToString());
                                    }
                                }

                                foreach (var option in groupData.Options)
                                {
                                    bool isSelected = activeOptions.Any(o => 
                                    {
                                        string trimmedO = o.Trim();
                                        string trimmedName = option.Name.Trim();
                                        if (trimmedO.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
                                            return true;

                                        if (int.TryParse(trimmedO, out int idx))
                                        {
                                            int optionIdx = groupData.Options.IndexOf(option);
                                            if (optionIdx == idx)
                                                return true;
                                        }
                                        return false;
                                    });

                                    if (isSelected && option.Files != null)
                                    {
                                        foreach (var kvp in option.Files)
                                        {
                                            string normalizedKey = kvp.Key.ToLowerInvariant().Replace("\\", "/");
                                            if (IsRelevantGamePath(normalizedKey))
                                                files[normalizedKey] = kvp.Value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (!handledByMeta)
            {
                // Default mod files
                string defaultJson = Path.Combine(modPath, "default_mod.json");
                if (File.Exists(defaultJson))
                {
                    try
                    {
                        string rawJson = File.ReadAllText(defaultJson);

                        // If this mod contains any non-texture file types, skip it entirely
                        if (rawJson.Contains(".scd") || rawJson.Contains(".pap") || rawJson.Contains(".vfx") || rawJson.Contains(".avfx") || rawJson.Contains(".tmb") || rawJson.Contains(".sklb") || rawJson.Contains(".shpk"))
                            return files;

                        var modData = JsonConvert.DeserializeObject<PenumbraModData>(rawJson);
                        if (modData?.Files != null)
                        {
                            foreach (var kvp in modData.Files)
                            {
                                string normalizedKey = kvp.Key.ToLowerInvariant().Replace("\\", "/");
                                if (IsRelevantGamePath(normalizedKey))
                                    files[normalizedKey] = kvp.Value;
                            }
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
                        if (groupData != null)
                        {
                            var activeOptionsPair = settings.FirstOrDefault(s => s.Key.Equals(groupData.Name, StringComparison.OrdinalIgnoreCase));
                            var activeOptions = activeOptionsPair.Value;
                            if (activeOptions == null)
                            {
                                activeOptions = new List<string>();
                                if (groupData.Type != null && groupData.Type.Equals("Multi", StringComparison.OrdinalIgnoreCase))
                                {
                                    for (int i = 0; i < groupData.Options.Count; i++)
                                    {
                                        if ((groupData.DefaultSettings & (1L << i)) != 0)
                                        {
                                            activeOptions.Add(i.ToString());
                                        }
                                    }
                                }
                                else
                                {
                                    activeOptions.Add(groupData.DefaultSettings.ToString());
                                }
                            }

                            foreach (var option in groupData.Options)
                            {
                                bool isSelected = activeOptions.Any(o => 
                                {
                                    string trimmedO = o.Trim();
                                    string trimmedName = option.Name.Trim();
                                    if (trimmedO.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
                                        return true;

                                    if (int.TryParse(trimmedO, out int idx))
                                    {
                                        int optionIdx = groupData.Options.IndexOf(option);
                                        if (optionIdx == idx)
                                            return true;
                                    }
                                    return false;
                                });

                                if (isSelected && option.Files != null)
                                {
                                    foreach (var kvp in option.Files)
                                    {
                                        string normalizedKey = kvp.Key.ToLowerInvariant().Replace("\\", "/");
                                        if (IsRelevantGamePath(normalizedKey))
                                            files[normalizedKey] = kvp.Value;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            }

            return files;
        }

        private class PenumbraModData { public Dictionary<string, string> Files { get; set; } }
        private class PenumbraModMetaV4 {
            public PenumbraModData DefaultData { get; set; }
            public List<PenumbraGroupData> Groups { get; set; }
        }
        private class PenumbraGroupData
        {
            public string Name { get; set; }
            public List<PenumbraOptionData> Options { get; set; }
            public long DefaultSettings { get; set; }
            public string Type { get; set; }
        }
        private class PenumbraOptionData
        {
            public string Name { get; set; }
            public Dictionary<string, string> Files { get; set; }
        }

        public static string FindMeshDiskPathInModDirectory(string targetKeyword, string targetGamePath)
        {
            var list = FindAllMeshDiskPathsInModDirectory(targetKeyword, targetGamePath);
            return list.Count > 0 ? list[0].DiskPath : null;
        }

        public static List<(string ModName, string DiskPath)> FindAllMeshDiskPathsInModDirectory(string targetKeyword, string targetGamePath)
        {
            var priorityResults = new List<(string ModName, string DiskPath)>();
            var otherResults = new List<(string ModName, string DiskPath)>();
            try
            {
                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
                string modDir = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                targetGamePath = targetGamePath.ToLowerInvariant().Replace("\\", "/");

                foreach (var mod in mods)
                {
                    bool matchesKeyword = !string.IsNullOrEmpty(targetKeyword) && mod.Value.ToLower().Contains(targetKeyword);
                    string modPath = Path.Combine(modDir, mod.Key);
                    bool foundInThisMod = false;

                    // Quick-skip mods that contain audio/animation/VFX files, not our domain
                    string quickCheckJson = Path.Combine(modPath, "default_mod.json");
                    if (File.Exists(quickCheckJson))
                    {
                        try
                        {
                            string rawJson = File.ReadAllText(quickCheckJson);
                            bool hasIrrelevant = rawJson.Contains(".scd") || rawJson.Contains(".pap") || rawJson.Contains(".vfx") || rawJson.Contains(".avfx") || rawJson.Contains(".tmb") || rawJson.Contains(".sklb") || rawJson.Contains(".shpk");
                            if (hasIrrelevant) continue;
                        }
                        catch { }
                    }
                    
                    // Try direct file first
                    string directPath = Path.Combine(modPath, targetGamePath.Replace("/", "\\"));
                    if (File.Exists(directPath))
                    {
                        if (matchesKeyword) priorityResults.Add((mod.Value, directPath));
                        else otherResults.Add((mod.Value, directPath));
                        continue; // skip JSON checks if direct file is found
                    }

                    // Check meta.json
                    bool handledByMeta = false;
                    string metaJson = Path.Combine(modPath, "meta.json");
                    if (File.Exists(metaJson))
                    {
                        try
                        {
                            string rawJson = File.ReadAllText(metaJson);
                            if (rawJson.Contains("\"DefaultData\"") || rawJson.Contains("\"Groups\""))
                            {
                                handledByMeta = true;
                                var meta = JsonConvert.DeserializeObject<PenumbraModMetaV4>(rawJson);
                                if (meta?.DefaultData?.Files != null)
                                {
                                    foreach (var kvp in meta.DefaultData.Files)
                                    {
                                        if (kvp.Key.ToLowerInvariant().Replace("\\", "/") == targetGamePath)
                                        {
                                            string mappedPath = Path.Combine(modPath, kvp.Value.Replace("/", "\\"));
                                            if (File.Exists(mappedPath))
                                            {
                                                if (matchesKeyword) priorityResults.Add(($"{mod.Value} (Default)", mappedPath));
                                                else otherResults.Add(($"{mod.Value} (Default)", mappedPath));
                                                foundInThisMod = true;
                                            }
                                        }
                                    }
                                }

                                if (meta?.Groups != null)
                                {
                                    foreach (var groupData in meta.Groups)
                                    {
                                        if (groupData?.Options != null)
                                        {
                                            foreach (var option in groupData.Options)
                                            {
                                                if (option.Files != null)
                                                {
                                                    foreach (var kvp in option.Files)
                                                    {
                                                        if (kvp.Key.ToLowerInvariant().Replace("\\", "/") == targetGamePath)
                                                        {
                                                            string mappedPath = Path.Combine(modPath, kvp.Value.Replace("/", "\\"));
                                                            if (File.Exists(mappedPath))
                                                            {
                                                                if (matchesKeyword) priorityResults.Add(($"{mod.Value} ({groupData.Name}: {option.Name})", mappedPath));
                                                                else otherResults.Add(($"{mod.Value} ({groupData.Name}: {option.Name})", mappedPath));
                                                                foundInThisMod = true;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (!handledByMeta)
                    {
                        // Check default_mod.json
                    string defaultJson = Path.Combine(modPath, "default_mod.json");
                    if (File.Exists(defaultJson))
                    {
                        try
                        {
                            string jsonText = File.ReadAllText(defaultJson);
                            if (jsonText.Contains(targetGamePath))
                            {
                                var modData = JsonConvert.DeserializeObject<PenumbraModData>(jsonText);
                                if (modData?.Files != null)
                                {
                                    foreach (var kvp in modData.Files)
                                    {
                                        if (kvp.Key.ToLowerInvariant().Replace("\\", "/") == targetGamePath)
                                        {
                                            string mappedPath = Path.Combine(modPath, kvp.Value.Replace("/", "\\"));
                                            if (File.Exists(mappedPath))
                                            {
                                                if (matchesKeyword) priorityResults.Add(($"{mod.Value} (Default)", mappedPath));
                                                else otherResults.Add(($"{mod.Value} (Default)", mappedPath));
                                                foundInThisMod = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    // Check all group JSONs
                    if (Directory.Exists(modPath))
                    {
                        foreach (var groupFile in Directory.GetFiles(modPath, "group_*.json"))
                        {
                            try
                            {
                                string groupText = File.ReadAllText(groupFile);
                                if (groupText.Contains(targetGamePath))
                                {
                                    var groupData = JsonConvert.DeserializeObject<PenumbraGroupData>(groupText);
                                    if (groupData?.Options != null)
                                    {
                                        foreach (var option in groupData.Options)
                                        {
                                            if (option.Files != null)
                                            {
                                                foreach (var kvp in option.Files)
                                                {
                                                    if (kvp.Key.ToLowerInvariant().Replace("\\", "/") == targetGamePath)
                                                    {
                                                        string mappedPath = Path.Combine(modPath, kvp.Value.Replace("/", "\\"));
                                                        if (File.Exists(mappedPath))
                                                        {
                                                            if (matchesKeyword) priorityResults.Add(($"{mod.Value} ({groupData.Name}: {option.Name})", mappedPath));
                                                            else otherResults.Add(($"{mod.Value} ({groupData.Name}: {option.Name})", mappedPath));
                                                            foundInThisMod = true;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            }
            catch { }
            priorityResults.AddRange(otherResults);
            return priorityResults;
        }
    }
}
