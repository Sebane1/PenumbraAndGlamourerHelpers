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

namespace PenumbraAndGlamourerHelpers
{
    public static class PenumbraAndGlamourerHelperFunctions
    {
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
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
                case FullEquipType.Unknown:
                    return ApiEquipSlot.Unknown;
                case FullEquipType.Head:
                    return ApiEquipSlot.Head;
                case FullEquipType.Body:
                    return ApiEquipSlot.Body;
                case FullEquipType.Hands:
                    return ApiEquipSlot.Hands;
                case FullEquipType.Legs:
                    return ApiEquipSlot.Legs;
                case FullEquipType.Feet:
                    return ApiEquipSlot.Feet;
                case FullEquipType.Ears:
                    return ApiEquipSlot.Ears;
                case FullEquipType.Wrists:
                    return ApiEquipSlot.Wrists;
                case FullEquipType.Finger:
                    return ApiEquipSlot.RFinger;
                default:
                    return ApiEquipSlot.Unknown;
            }
        }

        public static int GetRace(ICharacter playerCharacter)
        {
            try
            {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.Race.Value;
            }
            catch
            {
                return playerCharacter.Customize[(int)CustomizeIndex.Race];
            }
        }

        public static int GetTribe(ICharacter playerCharacter)
        {
            try
            {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.Clan.Value;
            }
            catch
            {
                if (playerCharacter != null)
                {
                    return playerCharacter.Customize[(int)CustomizeIndex.Tribe];
                }
                else
                {
                    return 0;
                }
            }
        }

        public static int GetGender(ICharacter playerCharacter)
        {
            try
            {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.Gender.Value;
            }
            catch
            {
                if (playerCharacter != null)
                {
                    return playerCharacter.Customize[(int)CustomizeIndex.Gender];
                }
                else
                {
                    return 0;
                }
            }
        }

        public static CharacterCustomization GetCustomization(ICharacter playerCharacter)
        {
            try
            {
                return CharacterCustomization.ReadCustomization(PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex).Item2);
            }
            catch
            {
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
        }

        public static void SetCustomization(ICharacter character, CharacterCustomization characterCustomization)
        {
            PenumbraAndGlamourerIpcWrapper.Instance.ApplyState.Invoke(characterCustomization.ToBase64(), character.ObjectIndex, 0, ApplyFlag.Customization);
        }
        public static Dictionary<Guid, string> GetGlamourerDesigns()
        {
            try
            {
                var glamourerDesignList = PenumbraAndGlamourerIpcWrapper.Instance.GetDesignList.Invoke();
                return glamourerDesignList;
            }
            catch (Exception e)
            {
                return new Dictionary<Guid, string>();
            }
        }
        public static bool IsHumanoid(ICharacter playerCharacter)
        {
            try
            {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.ModelId < 5;
            }
            catch
            {
                var modelType = playerCharacter.Customize[(int)CustomizeIndex.ModelType];
                return modelType is not 0 && modelType < 5;
            }
        }
        public static void SetClothingMod(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true)
        {
            Log.Debug("Attempting to find mods that contain \"" + modelMod + "\" (Set Clothing Mod).");
            int highestPriority = 10;
            foreach (string modName in modelMods)
            {
                if (modName.ToLower().Contains(modelMod.ToLower()))
                {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                }
                else
                {
                    if (CheckIfValidToChange(modName, modelMods))
                    {
                        if (disableOtherMods)
                        {
                            var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                        }
                        else
                        {
                            PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                        }
                    }
                }
            }
        }

        public static Dictionary<string, object> GetChangedItemsForMod(string modelMod, ICollection<string> modelMods)
        {
            Log.Debug("Attempting to find mods that contain \"" + modelMod + "\" (Getting Changed Items).");
            int lowestPriority = 10;
            foreach (string modName in modelMods)
            {
                if (modName.ToLower().Contains(modelMod.ToLower()))
                {
                    return PenumbraAndGlamourerIpcWrapper.Instance.GetChangedItemsForMod.Invoke("", modName);
                }
            }
            return new Dictionary<string, object>();
        }
        public static void SetBodyDependancies(Guid collection, ICollection<string> modelDependancies)
        {
            //int lowestPriority = 10;
            //foreach (string modName in modelDependancies) {
            //    var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
            //    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
            //}
        }
        public static void SetDependancies(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true)
        {
            Dictionary<string, bool> alreadyDisabled = new Dictionary<string, bool>();
            Log.Debug("Attempting to find mod dependancies that contain \"" + modelMod + "\" (Set Dependancies).");
            int lowestPriority = 10;
            foreach (string modName in modelMods)
            {
                if (modName.ToLower().Contains(modelMod.ToLower()))
                {
                    var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                }
                else
                {
                    if (FindStringMatch(modelMod, modName))
                    {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 10);
                    }
                    else if (disableOtherMods && CheckIfValidToChange(modName, modelMods))
                    {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                    }
                    else if (CheckIfValidToChange(modName, modelMods))
                    {
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                    }
                }
            }
        }

        public static bool CheckIfValidToChange(string mod, ICollection<string> modelMods)
        {
            var items = GetChangedItemsForMod(mod, modelMods).Values;
            foreach (var changedItem in items)
            {
                try
                {
                    string equipItemJson = JsonConvert.SerializeObject(changedItem,
                new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, PreserveReferencesHandling = PreserveReferencesHandling.Objects });
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
                catch (Exception e)
                {
                    Log.Debug(e, e.Message);
                }
            }
            return true;
        }

        public static bool FindStringMatch(string sourceMod, string comparisonMod)
        {
            string[] strings = sourceMod.Split(' ');
            foreach (string value in strings)
            {
                string loweredValue = value.ToLower();
                if (comparisonMod.ToLower().Contains(loweredValue)
                  && loweredValue.Length > 4 && !loweredValue.Contains("[") && !loweredValue.Contains("]")
                  && !loweredValue.Contains("by") && !loweredValue.Contains("update")
                  && !loweredValue.Contains("megapack") && !comparisonMod.Contains("megapack"))
                {
                    return true;
                }
            }
            return false;
        }
        public static void CleanSlate(Guid collection, ICollection<string> modelMods, ICollection<string> modelDepandacies)
        {
            string foundModName = "";
            if (collection == Guid.Empty)
            {
                collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(0).EffectiveCollection.Id;
            }
            Dictionary<string, bool> alreadyDisabled = new Dictionary<string, bool>();
            foreach (string modName in modelMods)
            {
                if (CheckIfValidToChange(modName, modelMods))
                {
                    var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, "", false, modName);
                }
            }
            SetBodyDependancies(collection, modelDepandacies);
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
                    bool isOwnMod = lowerValue.Contains("drag and drop") || lowerKey.Contains("drag and drop") || lowerValue.Contains("loosetexturecompilerdlc") || lowerKey.Contains("loosetexturecompilerdlc");
                    
                    if (!isOwnMod)
                    {
                        string[] categories = { "body", "face", "eyes", "eyebrows" };
                        foreach (var cat in categories)
                        {
                            if (lowerValue.EndsWith("texture " + cat) || lowerKey.EndsWith("texture" + cat) || lowerKey.EndsWith("texture " + cat))
                            {
                                isOwnMod = true;
                                break;
                            }
                        }
                    }
                    if (isOwnMod) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == Penumbra.Api.Enums.PenumbraApiEc.Success && settings.Item2.HasValue)
                    {
                        if (settings.Item2.Value.Item1 == true && settings.Item2.Value.Item2 < 100)
                        {
                            activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2));
                        }
                    }
                }

                // Sort by priority descending
                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                foreach (var mod in activeMods)
                {
                    bool foundBodyTextures = false;

                    // Check default_mod.json
                    string defaultJsonPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, "default_mod.json");
                    if (System.IO.File.Exists(defaultJsonPath))
                    {
                        string json = System.IO.File.ReadAllText(defaultJsonPath);
                        if (System.Text.RegularExpressions.Regex.IsMatch(json, @"(?:b0001|bibo|tbse|gen3|eve|yab)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            foundBodyTextures = true;
                            if (CheckIfJsonIsBodyType(mod.Name, mod.Dir, json, out int type))
                            {
                                detectedModName = mod.Name;
                                Log?.Information($"[Drag And Drop Texturing] Penumbra base body detected as {type} via '{mod.Name}' in default_mod.json");
                                return type;
                            }
                        }
                    }
                    
                    // Check group_*.json files
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(modDirectoryPath, mod.Dir)))
                    {
                        var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(modDirectoryPath, mod.Dir), "group_*.json");
                        foreach (var file in files)
                        {
                            string json = System.IO.File.ReadAllText(file);
                            if (System.Text.RegularExpressions.Regex.IsMatch(json, @"(?:b0001|bibo|tbse|gen3|eve|yab)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                foundBodyTextures = true;
                                if (CheckIfJsonIsBodyType(mod.Name, mod.Dir, json, out int type))
                                {
                                    detectedModName = mod.Name;
                                    Log?.Information($"[Drag And Drop Texturing] Penumbra base body detected as {type} via '{mod.Name}' in {System.IO.Path.GetFileName(file)}");
                                    return type;
                                }
                            }
                        }
                    }

                    // If it modifies bodies but we couldn't figure out which one, we keep searching lower priorities
                    if (foundBodyTextures)
                    {
                        Log?.Information($"[Drag And Drop Texturing] Penumbra mod '{mod.Name}' modifies body, but type (Bibo/Gen3) could not be determined.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Warning(ex, "Failed to detect base body from Penumbra");
            }
            Log?.Information($"[Drag And Drop Texturing] Penumbra base body detection returned unknown (-1).");
            return -1; // Unknown
        }

        private static bool CheckIfJsonIsBodyType(string name, string dir, string json, out int type)
        {
            type = 2;
            string lowerName = name.ToLower();
            string lowerDir = dir.ToLower();
            string lowerJson = json.ToLower();

            if (lowerName.Contains("gen3") || lowerName.Contains("eve") || lowerDir.Contains("gen3"))
            {
                type = 2; return true;
            }
            if (lowerName.Contains("tbse") || lowerDir.Contains("tbse"))
            {
                type = 3; return true;
            }
            if (lowerName.Contains("yab") || lowerDir.Contains("yab"))
            {
                type = 1; return true;
            }
            if (lowerName.Contains("bibo") || lowerName.Contains("b+") || lowerDir.Contains("bibo"))
            {
                type = 1; return true;
            }

            if (lowerJson.Contains("gen3") || lowerJson.Contains("eve"))
            {
                type = 2; return true;
            }
            if (lowerJson.Contains("tbse"))
            {
                type = 3; return true;
            }
            if (lowerJson.Contains("yab") || lowerJson.Contains("bibo"))
            {
                type = 1; return true;
            }

            return false;
        }

        public static void ExtractActiveTextureFromPenumbra(Guid collectionId, string category, string raceCode, string subRaceName, out string extractedModName, out string extractedBase, out string extractedNormal, out string extractedMask, FFXIVLooseTextureCompiler.PathOrganization.TextureSet item = null)
        {
            extractedModName = "";
            extractedBase = "";
            extractedNormal = "";
            extractedMask = "";
            try
            {
                var mods = PenumbraAndGlamourerIpcWrapper.Instance.GetModList.Invoke();
                string modDirectoryPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();

                List<(string Name, string Dir, int Priority, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> Settings)> activeMods = new List<(string Name, string Dir, int Priority, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> Settings)>();

                foreach (var mod in mods)
                {
                    string lowerKey = mod.Key.ToLower();
                    string lowerValue = mod.Value.ToLower();
                    bool isOwnMod = lowerValue.Contains("drag and drop") || lowerKey.Contains("drag and drop") || lowerValue.Contains("loosetexturecompilerdlc") || lowerKey.Contains("loosetexturecompilerdlc");
                    
                    if (!isOwnMod)
                    {
                        string[] categories = { "body", "face", "eyes", "eyebrows" };
                        foreach (var cat in categories)
                        {
                            if (lowerValue.EndsWith("texture " + cat) || lowerKey.EndsWith("texture" + cat) || lowerKey.EndsWith("texture " + cat))
                            {
                                isOwnMod = true;
                                break;
                            }
                        }
                    }
                    if (isOwnMod) continue;

                    var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collectionId, mod.Key, mod.Value, true);
                    if (settings.Item1 == Penumbra.Api.Enums.PenumbraApiEc.Success && settings.Item2.HasValue)
                    {
                        if (settings.Item2.Value.Item1 == true && settings.Item2.Value.Item2 < 100)
                        {
                            activeMods.Add((mod.Value, mod.Key, settings.Item2.Value.Item2, settings.Item2.Value.Item3));
                        }
                    }
                }

                activeMods.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                string strictPattern = "";
                string clanPattern = "";
                string fallbackPattern = "";
                if (category.EndsWith("_body") || category.Contains("fallback_Body"))
                {
                    strictPattern = @"chara/human/c" + raceCode + @"[^\""]*b0001[^\""]*(?:_d|_base|_b|_diffuse|_diff)\.tex";
                    clanPattern = @"chara/[^\""]*" + subRaceName + @"[^\""]*(?:_d|_base|_b|_diffuse|_diff)\.tex";
                    fallbackPattern = @"chara/[^\""]*(?:bibo|tbse|gen3|eve|yab|b0001|body|base)[^\""]*(?:_d|_base|_b|_diffuse|_diff)\.tex";
                }
                else if (category.EndsWith("_face") || category.Contains("fallback_Face"))
                {
                    strictPattern = @"chara/human/c" + raceCode + @"[^\""]*f\d{4}[^\""]*(?:_d|_base|_b|_fac_b)\.tex";
                    fallbackPattern = @"chara/[^\""]*f\d{4}[^\""]*(?:_d|_base|_b|_fac_b)\.tex";
                }
                else if (category.EndsWith("_eyes") || category.Contains("fallback_Eyes"))
                {
                    return; // Eyes should not pull underlays
                }
                else return;

                foreach (var mod in activeMods)
                {
                    bool isPapMod = false;
                    string defaultJsonPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, "default_mod.json");
                    string defaultJson = "";
                    
                    if (System.IO.File.Exists(defaultJsonPath))
                    {
                        defaultJson = System.IO.File.ReadAllText(defaultJsonPath);
                        if (defaultJson.Contains(".pap")) isPapMod = true;
                    }

                    if (!isPapMod && System.IO.Directory.Exists(System.IO.Path.Combine(modDirectoryPath, mod.Dir)))
                    {
                        var groupFiles = System.IO.Directory.GetFiles(System.IO.Path.Combine(modDirectoryPath, mod.Dir), "group_*.json");
                        foreach (var groupFile in groupFiles)
                        {
                            if (System.IO.File.ReadAllText(groupFile).Contains(".pap"))
                            {
                                isPapMod = true;
                                break;
                            }
                        }
                    }
                    
                    if (isPapMod) continue;

                    Dictionary<string, string> activeFiles = new Dictionary<string, string>();

                    // 1. Load default files
                    if (!string.IsNullOrEmpty(defaultJson))
                    {
                        try
                        {
                            var option = Newtonsoft.Json.JsonConvert.DeserializeObject<FFXIVVoicePackCreator.Json.Option>(defaultJson);
                            if (option?.Files != null)
                            {
                                foreach (var kvp in option.Files) activeFiles[kvp.Key] = kvp.Value;
                            }
                        }
                        catch { }
                    }

                    // 2. Load group overrides (these take precedence in Penumbra)
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(modDirectoryPath, mod.Dir)))
                    {
                        var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(modDirectoryPath, mod.Dir), "group_*.json");
                        foreach (var file in files)
                        {
                            try
                            {
                                string json = System.IO.File.ReadAllText(file);
                                var group = Newtonsoft.Json.JsonConvert.DeserializeObject<FFXIVVoicePackCreator.Json.Group>(json);
                                if (group != null && mod.Settings.ContainsKey(group.Name))
                                {
                                    var activeOptions = mod.Settings[group.Name];
                                    foreach (var option in group.Options)
                                    {
                                        if (activeOptions.Contains(option.Name) && option.Files != null)
                                        {
                                            foreach (var kvp in option.Files) activeFiles[kvp.Key] = kvp.Value;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // 3. Evaluate patterns in hierarchical order to ensure subrace textures aren't bypassed
                    string foundMatch = null;
                    
                    // Pass 0: Exact path match if item is provided
                    if (item != null && activeFiles.TryGetValue(item.InternalBasePath, out string exactMatch))
                    {
                        string fullPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, exactMatch.Replace("/", "\\"));
                        if (System.IO.File.Exists(fullPath))
                        {
                            extractedModName = mod.Name;
                            extractedBase = fullPath;
                            
                            if (!string.IsNullOrEmpty(item.InternalNormalPath) && activeFiles.TryGetValue(item.InternalNormalPath, out string foundNormal))
                            {
                                string normPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, foundNormal.Replace("/", "\\"));
                                if (System.IO.File.Exists(normPath)) extractedNormal = normPath;
                            }
                            
                            if (!string.IsNullOrEmpty(item.InternalMaskPath) && activeFiles.TryGetValue(item.InternalMaskPath, out string foundMask))
                            {
                                string maskPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, foundMask.Replace("/", "\\"));
                                if (System.IO.File.Exists(maskPath)) extractedMask = maskPath;
                            }
                            
                            return;
                        }
                    }
                    
                    // Pass 1: Clan Match (e.g. Raen vs Xaela for unified bodies like Pythia)
                    if (!string.IsNullOrEmpty(clanPattern))
                    {
                        foreach (var kvp in activeFiles)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(kvp.Key, clanPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                foundMatch = kvp.Value; break;
                            }
                        }
                    }

                    // Pass 2: Universal Fallback (e.g. Bibo / TBSE / Gen3)
                    if (foundMatch == null)
                    {
                        foreach (var kvp in activeFiles)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(kvp.Key, fallbackPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                foundMatch = kvp.Value; break;
                            }
                        }
                    }

                    // Pass 3: Strict Race/Gender Match (Vanilla Paths)
                    if (foundMatch == null)
                    {
                        foreach (var kvp in activeFiles)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(kvp.Key, strictPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                foundMatch = kvp.Value; break;
                            }
                        }
                    }

                    if (foundMatch != null)
                    {
                        string fullPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, foundMatch.Replace("/", "\\"));
                        if (System.IO.File.Exists(fullPath))
                        {
                            extractedModName = mod.Name;
                            extractedBase = fullPath;
                            
                            // Try to extract matching normal and mask
                            string prefix = System.Text.RegularExpressions.Regex.Replace(foundMatch, @"(?:_d|_base|_b|_diffuse|_diff|_fac_b)\.tex$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            
                            foreach (var kvp in activeFiles)
                            {
                                if (kvp.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (System.Text.RegularExpressions.Regex.IsMatch(kvp.Value, @"(?:_n|_norm)\.tex$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    {
                                        string normPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, kvp.Value.Replace("/", "\\"));
                                        if (System.IO.File.Exists(normPath)) extractedNormal = normPath;
                                    }
                                    else if (System.Text.RegularExpressions.Regex.IsMatch(kvp.Value, @"(?:_s|_mask|_m)\.tex$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    {
                                        string maskPath = System.IO.Path.Combine(modDirectoryPath, mod.Dir, kvp.Value.Replace("/", "\\"));
                                        if (System.IO.File.Exists(maskPath)) extractedMask = maskPath;
                                    }
                                }
                            }
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Warning(ex, "Failed to extract active texture from Penumbra");
            }
        }
    }
}
