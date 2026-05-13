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
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] DetectBaseBody: {activeMods.Count} active mods found.");

                foreach (var mod in activeMods)
                {
                    // Skip our own generated mods
                    string lnm = mod.Name.ToLower();
                    string ldr = mod.Dir.ToLower();
                    if (lnm.Contains("texture body") || lnm.Contains("texture face") || lnm.Contains("texture eyes") || lnm.Contains("texture eyebrows") || lnm.Contains("texture mod") || ldr.Contains("do_not_edit"))
                    {
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Skipping own mod: '{mod.Name}'");
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
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via paths: '{mod.Name}' -> type {result}");
                        return result;
                    }
                    else if (bodyTypeCount > 1)
                    {
                        // Multiple body types — check name first, then use whichever has more paths
                        detectedModName = mod.Name;
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Multiple body types in '{mod.Name}' (bibo={biboCount}, gen3={gen3Count}, tbse={tbseCount}). Checking name...");
                        if (lnm.Contains("bibo") || lnm.Contains("yab") || lnm.Contains("b+") || ldr.Contains("bibo")) return 1;
                        if (lnm.Contains("gen3") || lnm.Contains("tight & firm") || lnm.Contains("eve") || ldr.Contains("gen3")) return 2;
                        if (lnm.Contains("tbse") || ldr.Contains("tbse")) return 3;
                        // Name didn't help — the type with the most paths is the primary one
                        int max = Math.Max(biboCount, Math.Max(gen3Count, tbseCount));
                        int dominant = gen3Count == max ? 2 : biboCount == max ? 1 : 3;
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Name inconclusive. Dominant path count wins: type {dominant}");
                        return dominant;
                    }
                    else
                    {
                        // No body paths found — check mod name/dir as last resort
                        if (lnm.Contains("bibo") || lnm.Contains("yab") || lnm.Contains("b+") || ldr.Contains("bibo")) { detectedModName = mod.Name; plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> Bibo"); return 1; }
                        if (lnm.Contains("gen3") || lnm.Contains("tight & firm") || ldr.Contains("gen3")) { detectedModName = mod.Name; plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> Gen3"); return 2; }
                        if (lnm.Contains("tbse") || ldr.Contains("tbse")) { detectedModName = mod.Name; plugin?.PluginLog?.Information($"[Drag And Drop Debug] Body detected via name: '{mod.Name}' -> TBSE"); return 3; }
                    }
                }
            }
            catch (Exception ex) { Log?.Warning(ex, "Failed to detect base body from Penumbra"); }
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
                    // SKIP OUR OWN MODS — our generated mods always end with "Texture Body/Face/Eyes/Eyebrows"
                    string modNameLower = mod.Value.ToLower();
                    string modDirLower = mod.Key.ToLower();
                    if (modNameLower.Contains("drag and drop") || modDirLower.Contains("drag and drop") ||
                        modNameLower.EndsWith("texture body") || modNameLower.EndsWith("texture face") || modNameLower.EndsWith("texture eyes") ||
                        modNameLower.EndsWith("texture eyebrows") || modNameLower.EndsWith("texture mod")) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == PenumbraApiEc.Success && settings.Item2.HasValue && settings.Item2.Value.Item1)
                    {
                        activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                    }
                }
                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));
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

        public static void PopulateOmniOverrides(Guid collectionId, int gender, int race, DragAndDropTexturing.Plugin plugin)
        {
            // Clear prior static values to prevent stale reads
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.BiboOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.Gen3Override = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.Gen2Override = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.TbseOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.OtopopOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.VanillaLalaOverride = null;
            FFXIVLooseTextureCompiler.Export.BackupTexturePaths.RelalaOverride = null;

            if (!BackupTexturePaths.OverrideMode) return;

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
                        modNameLower.EndsWith("texture body") || modNameLower.EndsWith("texture face") || modNameLower.EndsWith("texture eyes") ||
                        modNameLower.EndsWith("texture eyebrows") || modNameLower.EndsWith("texture mod")) continue;
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

                    // Diagnostic: show what body types this mod contains
                    int biboKeys = 0, gen3Keys = 0, vanillaKeys = 0;
                    foreach (var key in files.Keys)
                    {
                        if (key.StartsWith("chara/bibo_")) biboKeys++;
                        if (key.Contains("tfgen3")) gen3Keys++;
                        if (key.Contains("obj/body") && !key.Contains("tfgen3") && !key.StartsWith("chara/bibo_")) vanillaKeys++;
                    }
                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Checking mod '{mod.Name}' (Priority: {mod.Priority}, Files: {files.Count}, Bibo: {biboKeys}, Gen3: {gen3Keys}, Vanilla: {vanillaKeys}) for Race {mainRace}...");

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

                    // After each mod, cross-convert any gaps between Bibo+/Gen3 immediately
                    // so lower-priority mods can't fill the slot with their own textures
                    CrossConvertMissingOverrides(plugin);
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
                    // Bibo+ exists, Gen3 is missing → convert Bibo+ to Gen3
                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert: Bibo+ '{BackupTexturePaths.BiboOverride.ModName}' → Gen3 (auto-generated)");

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
                    btp.ModName = BackupTexturePaths.BiboOverride.ModName + " (Auto-Converted Gen3)";
                    BackupTexturePaths.Gen3Override = btp;

                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert complete: Gen3 override set from Bibo+ source.");
                }
                else if (hasGen3 && !hasBibo)
                {
                    // Gen3 exists, Bibo+ is missing → convert Gen3 to Bibo+
                    plugin?.PluginLog?.Information($"[Drag And Drop Debug] Cross-convert: Gen3 '{BackupTexturePaths.Gen3Override.ModName}' → Bibo+ (auto-generated)");

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

            // --- Case 1: Override already exists but has gaps in normal/mask ---
            if (overrideField != null)
            {
                bool filled = false;
                if (overrideField.NeedsNormal)
                {
                    string normFullPath = FindTexture(1, "_norm.tex") ?? FindTexture(1, "_norm.tex", 0);
                    if (normFullPath != null)
                    {
                        overrideField.FillNormal(normFullPath);
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Gap-filled normal for BodyType {baseBody} from '{modName}'");
                        filled = true;
                    }
                }
                if (overrideField.NeedsMask)
                {
                    string maskFullPath = FindTexture(2, "_mask.tex") ?? FindTexture(2, "_mask.tex", 0);
                    if (maskFullPath != null)
                    {
                        overrideField.FillMask(maskFullPath);
                        plugin?.PluginLog?.Information($"[Drag And Drop Debug] Gap-filled mask for BodyType {baseBody} from '{modName}'");
                        filled = true;
                    }
                }
                return overrideField;
            }

            // --- Case 2: No override yet — try to claim the slot with a base texture ---
            string baseFullPath = FindTexture(0, "_base.tex");
            if (baseFullPath == null)
            {
                // Also try non-verbose suffixes for vanilla body types
                if (baseBody == 0 || baseBody == 3)
                    baseFullPath = FindTexture(0, "_d.tex");
            }

            if (baseFullPath == null)
            {
                plugin?.PluginLog?.Information($"[Drag And Drop Debug] No match for BodyType {baseBody} in '{modName}'");
                return overrideField;
            }



            plugin?.PluginLog?.Information($"[Drag And Drop Debug] Found override base for BodyType {baseBody} in '{modName}': {baseFullPath}");

            string normPath = FindTexture(1, "_norm.tex") ?? FindTexture(1, "_norm.tex", 0);
            string maskPath = FindTexture(2, "_mask.tex") ?? FindTexture(2, "_mask.tex", 0);

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
