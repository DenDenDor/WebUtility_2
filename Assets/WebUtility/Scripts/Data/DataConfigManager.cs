using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace WebUtility
{
    public static class DataConfigManager
    {
        private const string ConfigAddressPrefix = "Configs/";
        private const string ConfigLabel = "Config";

        private static bool _addressablesInitialized;
        private static bool _addressablesInitializing;
        
        private static readonly Dictionary<string, UnityEngine.Object> _preloadedUnityObjects = new Dictionary<string, UnityEngine.Object>();
        
        private static readonly Dictionary<string, AsyncOperationHandle<UnityEngine.Object>> _preloadHandles = new Dictionary<string, AsyncOperationHandle<UnityEngine.Object>>();
        
        public static void PreloadUnityObject(string address)
        {
            if (_preloadedUnityObjects.ContainsKey(address))
            {
                Debug.Log($"[DataConfigManager] Unity object {address} already preloaded");
                return;
            }
            
            if (!EnsureAddressablesInitialized())
            {
                Debug.LogWarning($"[DataConfigManager] Cannot preload Unity object {address} - Addressables not initialized");
                return;
            }
            
            try
            {
                #if UNITY_EDITOR
                Debug.Log($"[DataConfigManager] Starting synchronous preload of {address} in Editor...");
                var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
                var obj = handle.WaitForCompletion();
                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && obj != null)
                {
                    _preloadedUnityObjects[address] = obj;
                    Debug.Log($"[DataConfigManager] Preloaded Unity object in Editor: {address} ({obj.name})");
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                else
                {
                    Debug.LogWarning($"[DataConfigManager] Failed to preload Unity object {address} in Editor. Status: {handle.Status}, Exception: {handle.OperationException?.Message}");
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                #else
                Debug.Log($"[DataConfigManager] Starting async preload of {address} in build...");
                var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
                _preloadHandles[address] = handle;
                
                handle.Completed += (opHandle) =>
                {
                    if (opHandle.Status == AsyncOperationStatus.Succeeded && opHandle.Result != null)
                    {
                        _preloadedUnityObjects[address] = opHandle.Result;
                        Debug.Log($"[DataConfigManager] Preloaded Unity object in build: {address} ({opHandle.Result.name})");
                    }
                    else
                    {
                        Debug.LogWarning($"[DataConfigManager] Failed to preload Unity object {address} in build. Status: {opHandle.Status}, Exception: {opHandle.OperationException?.Message}");
                    }
                };
                #endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataConfigManager] Exception preloading Unity object {address}: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        public static T GetData<T>(Enum configType) where T : AbstractData
        {
            if (configType == null)
            {
                Debug.LogError("[DataConfigManager] Config type enum cannot be null");
                return null;
            }

            try
            {
                Type type = typeof(T);
                string configName = configType.ToString();
                return (T)GetData(type, configName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataConfigManager] Failed to get data for {configType}: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
        }

        private static object GetData(Type type, string configName)
        {
            if (type == null)
            {
                Debug.LogError("Type cannot be null");
                return null;
            }

            if (string.IsNullOrEmpty(configName))
            {
                Debug.LogError("Config name cannot be null or empty");
                return null;
            }

            if (!typeof(AbstractData).IsAssignableFrom(type))
            {
                Debug.LogError($"Type {type.Name} must inherit from AbstractData");
                return null;
            }

            if (!EnsureAddressablesInitialized())
            {
                Debug.LogError($"[DataConfigManager] Addressables not initialized. Cannot load config {configName}.");
                return null;
            }

            string address = BuildAddress(type.Name, configName);
            Debug.Log($"[DataConfigManager] Attempting to load config: {configName} at address: {address}");
            
            IList<IResourceLocation> locations;
            if (!TryLocateTextAsset(address, out locations))
            {
                Debug.LogWarning($"[DataConfigManager] Config address not found: {address}. Ensure configs are marked as Addressable via Tools/Addressables/Update Config Entries.");
                
                if (Addressables.ResourceLocators != null)
                {
                    var allLocations = Addressables.LoadResourceLocationsAsync(ConfigLabel, typeof(TextAsset));
                    allLocations.WaitForCompletion();
                    if (allLocations.Status == AsyncOperationStatus.Succeeded && allLocations.Result != null)
                    {
                        Debug.Log($"[DataConfigManager] Available config addresses ({allLocations.Result.Count}):");
                        foreach (var loc in allLocations.Result)
                        {
                            Debug.Log($"  - {loc.PrimaryKey}");
                        }
                    }
                    if (allLocations.IsValid())
                    {
                        Addressables.Release(allLocations);
                    }
                }
                
                return null;
            }
            
            Debug.Log($"[DataConfigManager] Config address found: {address}. Locations count: {locations?.Count ?? 0}");

            TextAsset configAsset = LoadConfigAsset(address);
            if (configAsset == null)
            {
                Debug.LogError($"[DataConfigManager] Failed to load config asset for {configName} at address {address}");
                return null;
            }

            Debug.Log($"[DataConfigManager] Config asset loaded, starting deserialization for {configName}...");

            try
            {
                if (string.IsNullOrEmpty(configAsset.text))
                {
                    Debug.LogError($"[DataConfigManager] Config asset text is empty for {configName}");
                    return null;
                }

                Debug.Log($"[DataConfigManager] Deserializing wrapper for {configName}...");
                var wrapper = JsonUtility.FromJson<DataConfigWrapper>(configAsset.text);
                if (wrapper == null)
                {
                    Debug.LogError($"[DataConfigManager] Failed to deserialize config wrapper for {configName}");
                    return null;
                }

                Debug.Log($"[DataConfigManager] Wrapper deserialized. TypeName: {wrapper.TypeName}, Name: {wrapper.Name}, HasObjectRefs: {!string.IsNullOrEmpty(wrapper.ObjectReferencesJson)}");

                if (wrapper.TypeName != type.Name)
                {
                    Debug.LogWarning($"[DataConfigManager] Config type mismatch. Expected {type.Name}, but got {wrapper.TypeName}");
                }

                if (string.IsNullOrEmpty(wrapper.JsonData))
                {
                    Debug.LogError($"[DataConfigManager] Config JSON data is empty for {configName}");
                    return null;
                }

                Debug.Log($"[DataConfigManager] Deserializing data object of type {type.Name}...");
                object data = JsonUtility.FromJson(wrapper.JsonData, type);
                if (data == null)
                {
                    Debug.LogError($"[DataConfigManager] Failed to deserialize data of type {type.Name} from config {configName}");
                    return null;
                }

                Debug.Log($"[DataConfigManager] Data object deserialized successfully for {configName}");

                if (!string.IsNullOrEmpty(wrapper.ObjectReferencesJson))
                {
                    Debug.Log($"[DataConfigManager] Restoring Unity object references for {configName}...");
                    try
                    {
                        RestoreUnityObjectReferences(data, type, wrapper.ObjectReferencesJson);
                        Debug.Log($"[DataConfigManager] Unity object references restored for {configName}");
                    }
                    catch (Exception refEx)
                    {
                        Debug.LogWarning($"[DataConfigManager] Failed to restore Unity object references for {configName}: {refEx.Message}");
                    }
                }
                else
                {
                    Debug.Log($"[DataConfigManager] No Unity object references to restore for {configName}");
                }

                Debug.Log($"[DataConfigManager] Config {configName} loaded successfully!");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataConfigManager] Failed to load config {configName}: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
        }

        public static bool ConfigExists<T>(Enum configType) where T : AbstractData
        {
            if (configType == null)
                return false;

            Type type = typeof(T);
            string configName = configType.ToString();
            EnsureAddressablesInitialized();
            string address = BuildAddress(type.Name, configName);
            return TryLocateTextAsset(address, out _);
        }

        public static T[] GetAllDataOfType<T>() where T : AbstractData
        {
            Type type = typeof(T);
            var results = new List<T>();

            if (!EnsureAddressablesInitialized())
            {
                Debug.LogError("[DataConfigManager] Addressables not initialized. Cannot load configs.");
                return results.ToArray();
            }

            var locations = LoadAllConfigLocations();
            if (locations == null || locations.Count == 0)
            {
                Debug.LogWarning($"[DataConfigManager] No config locations found for type {type.Name}");
                return results.ToArray();
            }

            foreach (var location in locations)
            {
                try
                {
                    string address = location.PrimaryKey;
                    if (!address.StartsWith(ConfigAddressPrefix))
                        continue;

                    string configKey = address.Substring(ConfigAddressPrefix.Length);
                    if (!configKey.StartsWith(type.Name + "_"))
                        continue;

                    string configName = configKey.Substring(type.Name.Length + 1);
                    T data = GetData<T>(configName);
                    if (data != null)
                    {
                        results.Add(data);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataConfigManager] Failed to load config from location {location.PrimaryKey}: {e.Message}");
                }
            }

            return results.ToArray();
        }

        public static T GetData<T>(string configName) where T : AbstractData
        {
            Type type = typeof(T);
            object data = GetData(type, configName);
            return data as T;
        }

        #region Addressables helpers

        private static bool EnsureAddressablesInitialized()
        {
            if (_addressablesInitialized)
                return true;

            if (_addressablesInitializing)
            {
                return false;
            }

            try
            {
                if (Addressables.ResourceLocators != null)
                {
                    var enumerator = Addressables.ResourceLocators.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        _addressablesInitialized = true;
                        Debug.Log("[DataConfigManager] Addressables already initialized (detected via ResourceLocators)");
                        return true;
                    }
                }
            }
            catch
            {
            }

            _addressablesInitializing = true;

            try
            {
                var handle = Addressables.InitializeAsync();
                
                if (!handle.IsValid())
                {
                    Debug.LogError("[DataConfigManager] Failed to get valid Addressables initialization handle");
                    return false;
                }

                try
                {
                    handle.WaitForCompletion();
                    
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                    
                    if (Addressables.ResourceLocators != null)
                    {
                        var enumerator = Addressables.ResourceLocators.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            _addressablesInitialized = true;
                            Debug.Log("[DataConfigManager] Addressables initialized successfully (verified via ResourceLocators)");
                        }
                        else
                        {
                            Debug.LogError("[DataConfigManager] Addressables initialization completed but no ResourceLocators found");
                        }
                    }
                    else
                    {
                        Debug.LogError("[DataConfigManager] Addressables initialization completed but ResourceLocators is null");
                    }
                }
                catch (Exception waitException)
                {
                    Debug.LogError($"[DataConfigManager] WaitForCompletion failed: {waitException.Message}");
                    
                    try
                    {
                        if (handle.IsValid())
                        {
                            Addressables.Release(handle);
                        }
                    }
                    catch
                    {
                    }
                    
                    throw;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataConfigManager] Failed to initialize Addressables: {e.Message}\nStackTrace: {e.StackTrace}");
                
                try
                {
                    if (Addressables.ResourceLocators != null)
                    {
                        var enumerator = Addressables.ResourceLocators.GetEnumerator();
                        if (enumerator.MoveNext())
                        {
                            _addressablesInitialized = true;
                            Debug.Log("[DataConfigManager] Addressables initialized after exception (detected via ResourceLocators)");
                        }
                    }
                }
                catch
                {
                }
            }
            finally
            {
                _addressablesInitializing = false;
            }

            return _addressablesInitialized;
        }

        private static bool TryLocateTextAsset(string address, out IList<IResourceLocation> locations)
        {
            locations = null;
            
            try
            {
                if (Addressables.ResourceLocators == null)
                {
                    Debug.LogWarning("[DataConfigManager] ResourceLocators is null. Addressables may not be initialized.");
                    return false;
                }

                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator != null && locator.Locate(address, typeof(TextAsset), out locations))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataConfigManager] Error locating text asset at {address}: {e.Message}");
            }

            return false;
        }

        private static TextAsset LoadConfigAsset(string address)
        {
            AsyncOperationHandle<TextAsset> handle = default;
            bool handleCreated = false;

            try
            {
                Debug.Log($"[DataConfigManager] Starting to load config asset: {address}");
                
                handle = Addressables.LoadAssetAsync<TextAsset>(address);
                handleCreated = handle.IsValid();

                if (!handleCreated)
                {
                    Debug.LogError($"[DataConfigManager] Failed to create load handle for address: {address}");
                    return null;
                }

                Debug.Log($"[DataConfigManager] Load handle created, waiting for completion...");

                TextAsset result = null;
                try
                {
                    result = handle.WaitForCompletion();
                    Debug.Log($"[DataConfigManager] WaitForCompletion finished for {address}");
                }
                catch (Exception waitEx)
                {
                    Debug.LogError($"[DataConfigManager] WaitForCompletion exception for {address}: {waitEx.Message}");
                    
                    if (handle.IsValid())
                    {
                        try
                        {
                            if (handle.Status == AsyncOperationStatus.Succeeded)
                            {
                                result = handle.Result;
                                Debug.Log($"[DataConfigManager] Got result after exception: {result != null}");
                            }
                            else
                            {
                                string errorMsg = handle.OperationException != null ? handle.OperationException.Message : "Unknown error";
                                Debug.LogError($"[DataConfigManager] Handle status is {handle.Status}, Error: {errorMsg}");
                            }
                        }
                        catch (Exception statusEx)
                        {
                            Debug.LogError($"[DataConfigManager] Exception checking handle status: {statusEx.Message}");
                        }
                    }
                    
                    if (result == null)
                    {
                        return null;
                    }
                }
                
                if (result == null && handle.IsValid())
                {
                    try
                    {
                        if (handle.Status == AsyncOperationStatus.Succeeded)
                        {
                            result = handle.Result;
                        }
                        else
                        {
                            string errorMsg = handle.OperationException != null ? handle.OperationException.Message : "Unknown error";
                            Debug.LogError($"[DataConfigManager] Failed to load config asset {address}. Status: {handle.Status}, Error: {errorMsg}");
                        }
                    }
                    catch (Exception statusEx)
                    {
                        Debug.LogError($"[DataConfigManager] Exception getting result: {statusEx.Message}");
                    }
                }
                
                if (result == null)
                {
                    Debug.LogWarning($"[DataConfigManager] Config asset is null for address: {address}");
                }
                else
                {
                    Debug.Log($"[DataConfigManager] Config asset loaded successfully: {address}, text length: {result.text?.Length ?? 0}");
                }
                
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataConfigManager] Exception loading config asset {address}: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
            finally
            {
                if (handleCreated && handle.IsValid())
                {
                    try
                    {
                        Addressables.Release(handle);
                        Debug.Log($"[DataConfigManager] Handle released for {address}");
                    }
                    catch (Exception releaseEx)
                    {
                        Debug.LogWarning($"[DataConfigManager] Exception releasing handle: {releaseEx.Message}");
                    }
                }
            }
        }

        private static IList<IResourceLocation> LoadAllConfigLocations()
        {
            AsyncOperationHandle<IList<IResourceLocation>> handle = default;
            bool handleCreated = false;

            try
            {
                handle = Addressables.LoadResourceLocationsAsync(ConfigLabel, typeof(TextAsset));
                handleCreated = handle.IsValid();

                if (!handleCreated)
                {
                    Debug.LogError("[DataConfigManager] Failed to create load handle for config locations");
                    return null;
                }

                handle.WaitForCompletion();
                
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    string errorMsg = handle.OperationException != null ? handle.OperationException.Message : "Unknown error";
                    Debug.LogError($"[DataConfigManager] Failed to load config locations. Status: {handle.Status}, Error: {errorMsg}");
                    return null;
                }

                return handle.Result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataConfigManager] Exception loading config locations: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
            finally
            {
                if (handleCreated && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static string BuildAddress(string typeName, string configName)
        {
            return $"{ConfigAddressPrefix}{typeName}_{configName}";
        }

        #endregion

        #region Unity object references restoration

        private static void RestoreUnityObjectReferences(object data, Type type, string referencesJson)
        {
            if (data == null || type == null || string.IsNullOrEmpty(referencesJson))
            {
                Debug.LogWarning("[DataConfigManager] RestoreUnityObjectReferences: Invalid parameters");
                return;
            }

            try
            {
                Debug.Log($"[DataConfigManager] Parsing Unity object references JSON (length: {referencesJson.Length})...");
                var references = JsonUtility.FromJson<ConfigObjectReferences>(referencesJson);
                if (references == null || references.references == null)
                {
                    Debug.LogWarning("[DataConfigManager] Failed to parse Unity object references or references list is null");
                    return;
                }

                Debug.Log($"[DataConfigManager] Found {references.references.Count} Unity object references to restore");

                int restoredCount = 0;
                foreach (var refData in references.references)
                {
                    try
                    {
                        Debug.Log($"[DataConfigManager] Loading Unity object for field: {refData.fieldPath}, address: {refData.address}, assetPath: {refData.assetPath}");
                        
                        UnityEngine.Object obj = LoadUnityObject(refData);
                        if (obj != null)
                        {
                            Debug.Log($"[DataConfigManager] Unity object loaded successfully: {obj.name} ({obj.GetType().Name}) for field {refData.fieldPath}");
                            SetFieldValueByPath(data, type, refData.fieldPath, obj);
                            restoredCount++;
                            Debug.Log($"[DataConfigManager] Field {refData.fieldPath} restored successfully");
                        }
                        else
                        {
                            Debug.LogWarning($"[DataConfigManager] Failed to load Unity object for field {refData.fieldPath} - object is null");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[DataConfigManager] Failed to restore reference {refData.fieldPath}: {e.Message}\nStackTrace: {e.StackTrace}");
                    }
                }

                Debug.Log($"[DataConfigManager] Unity object references restoration completed: {restoredCount}/{references.references.Count} restored");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataConfigManager] Failed to parse Unity object references: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        private static UnityEngine.Object LoadUnityObject(ObjectReferenceData reference)
        {
            if (reference == null)
            {
                Debug.LogWarning("[DataConfigManager] LoadUnityObject: reference is null");
                return null;
            }

            Debug.Log($"[DataConfigManager] LoadUnityObject: fieldPath={reference.fieldPath}, address={reference.address}, assetPath={reference.assetPath}, objectGuid={reference.objectGuid}, objectType={reference.objectType}");

            if (!EnsureAddressablesInitialized())
            {
                Debug.LogWarning("[DataConfigManager] LoadUnityObject: Addressables not initialized");
            }

            if (!string.IsNullOrEmpty(reference.address))
            {
                Debug.Log($"[DataConfigManager] Trying to load Unity object by address: {reference.address}");
                
                if (_preloadedUnityObjects.TryGetValue(reference.address, out UnityEngine.Object cachedObj))
                {
                    Debug.Log($"[DataConfigManager] Found Unity object in preloaded cache: {reference.address}");
                    return cachedObj;
                }
                
                #if !UNITY_EDITOR
                if (_preloadHandles.TryGetValue(reference.address, out AsyncOperationHandle<UnityEngine.Object> preloadHandle))
                {
                    if (preloadHandle.IsValid() && preloadHandle.Status == AsyncOperationStatus.Succeeded && preloadHandle.Result != null)
                    {
                        _preloadedUnityObjects[reference.address] = preloadHandle.Result;
                        Debug.Log($"[DataConfigManager] Found Unity object via preload handle: {reference.address}");
                        return preloadHandle.Result;
                    }
                }
                #endif
                
                var obj = LoadUnityObjectByAddress(reference.address);
                if (obj != null)
                {
                    Debug.Log($"[DataConfigManager] Successfully loaded Unity object by address: {reference.address}");
                    return obj;
                }
                Debug.LogWarning($"[DataConfigManager] Failed to load Unity object by address: {reference.address}");
            }
            
            if (string.IsNullOrEmpty(reference.assetPath))
            {
                Debug.LogWarning($"[DataConfigManager] Cannot load Unity object - no address or assetPath for field: {reference.fieldPath}");
                return null;
            }

            if (!string.IsNullOrEmpty(reference.assetPath))
            {
                if (reference.assetPath.Contains("unity_builtin_extra"))
                {
                    Debug.LogWarning($"[DataConfigManager] Cannot load built-in Unity resource: {reference.assetPath}. This resource is not available in runtime.");
                    return null;
                }
                
                Debug.Log($"[DataConfigManager] Trying to load Unity object by assetPath: {reference.assetPath}");
                var obj = LoadUnityObjectByAddress(reference.assetPath);
                if (obj != null)
                {
                    Debug.Log($"[DataConfigManager] Successfully loaded Unity object by assetPath: {reference.assetPath}");
                    return obj;
                }
                Debug.LogWarning($"[DataConfigManager] Failed to load Unity object by assetPath: {reference.assetPath}");
            }

            if (!string.IsNullOrEmpty(reference.objectGuid))
            {
                if (reference.objectGuid == "0000000000000000f000000000000000")
                {
                    Debug.LogWarning($"[DataConfigManager] Cannot load built-in Unity resource by GUID. This resource is not available in runtime.");
                    return null;
                }
                
                Debug.Log($"[DataConfigManager] Trying to load Unity object by objectGuid: {reference.objectGuid}");
                var obj = LoadUnityObjectByAddress(reference.objectGuid);
                if (obj != null)
                {
                    Debug.Log($"[DataConfigManager] Successfully loaded Unity object by objectGuid: {reference.objectGuid}");
                    return obj;
                }
                Debug.LogWarning($"[DataConfigManager] Failed to load Unity object by objectGuid: {reference.objectGuid}");
            }

            if (!string.IsNullOrEmpty(reference.assetPath))
            {
                Debug.Log($"[DataConfigManager] Trying to load Unity object from Resources: {reference.assetPath}");
                var obj = LoadUnityObjectFromResources(reference.assetPath, reference.objectType);
                if (obj != null)
                {
                    Debug.Log($"[DataConfigManager] Successfully loaded Unity object from Resources: {reference.assetPath}");
                    return obj;
                }
                Debug.LogWarning($"[DataConfigManager] Failed to load Unity object from Resources: {reference.assetPath}");
            }

            if (!string.IsNullOrEmpty(reference.address))
            {
                if (_preloadedUnityObjects.TryGetValue(reference.address, out UnityEngine.Object finalCachedObj))
                {
                    Debug.Log($"[DataConfigManager] Found Unity object in cache before returning null: {reference.address}");
                    return finalCachedObj;
                }
            }
            
            Debug.LogWarning($"[DataConfigManager] All loading methods failed for field: {reference.fieldPath}");
            return null;
        }

        private static UnityEngine.Object LoadUnityObjectByAddress(string address)
        {
            Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Checking if address exists in Addressables: {address}");
            
            if (_preloadedUnityObjects.TryGetValue(address, out UnityEngine.Object cachedObj))
            {
                Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Found in preloaded cache: {address}");
                return cachedObj;
            }
            
            IList<IResourceLocation> locations;
            if (!TryLocateUnityObject(address, out locations))
            {
                Debug.LogWarning($"[DataConfigManager] LoadUnityObjectByAddress: Address not found in Addressables: {address}");
                return null;
            }

            Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Address found, locations count: {locations?.Count ?? 0}");

            AsyncOperationHandle<UnityEngine.Object> handle = default;
            bool handleCreated = false;

            try
            {
                Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Creating load handle for {address}");
                handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
                handleCreated = handle.IsValid();

                if (!handleCreated)
                {
                    Debug.LogWarning($"[DataConfigManager] LoadUnityObjectByAddress: Failed to create valid handle for {address}");
                    return null;
                }

                Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Loading asset for {address}...");
                
                UnityEngine.Object result = null;
                
                try
                {
                    if (handle.IsValid())
                    {
                        if (handle.Status == AsyncOperationStatus.Succeeded)
                        {
                            result = handle.Result;
                            if (result != null && !_preloadedUnityObjects.ContainsKey(address))
                            {
                                _preloadedUnityObjects[address] = result;
                            }
                            Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Asset already loaded for {address}, result: {result != null}");
                        }
                        else if (handle.Status == AsyncOperationStatus.Failed)
                        {
                            string errorMsg = handle.OperationException != null ? handle.OperationException.Message : "Unknown error";
                            Debug.LogError($"[DataConfigManager] LoadUnityObjectByAddress: Handle failed for {address}: {errorMsg}");
                            result = null;
                        }
                        else
                        {
                            #if UNITY_EDITOR
                            Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Waiting for completion in Editor for {address}...");
                            result = handle.WaitForCompletion();
                            
                            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
                            {
                                result = handle.Result;
                                if (result != null && !_preloadedUnityObjects.ContainsKey(address))
                                {
                                    _preloadedUnityObjects[address] = result;
                                }
                                Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Asset loaded in Editor for {address}, result: {result != null}");
                            }
                            else if (handle.IsValid() && handle.Status == AsyncOperationStatus.Failed)
                            {
                                string errorMsg = handle.OperationException != null ? handle.OperationException.Message : "Unknown error";
                                Debug.LogError($"[DataConfigManager] LoadUnityObjectByAddress: Handle failed after WaitForCompletion for {address}: {errorMsg}");
                                result = null;
                            }
                            #else
                            if (_preloadedUnityObjects.TryGetValue(address, out cachedObj))
                            {
                                Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Found in cache after handle creation for {address}");
                                result = cachedObj;
                            }
                            else
                            {
                                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
                                {
                                    result = handle.Result;
                                    if (result != null && !_preloadedUnityObjects.ContainsKey(address))
                                    {
                                        _preloadedUnityObjects[address] = result;
                                    }
                                    Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Asset loaded successfully for {address} (handle completed)");
                                }
                                else
                                {
                                    if (_preloadedUnityObjects.TryGetValue(address, out cachedObj))
                                    {
                                        Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Found in cache after status check for {address}");
                                        result = cachedObj;
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[DataConfigManager] LoadUnityObjectByAddress: Asset not ready yet for {address}. Status: {handle.Status}. Cannot wait synchronously in builds. Asset should be preloaded.");
                                        result = null;
                                        
                                        handleCreated = false;
                                    }
                                }
                            }
                            #endif
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DataConfigManager] LoadUnityObjectByAddress: Exception during load for {address}: {e.Message}\nStackTrace: {e.StackTrace}");
                    result = null;
                }
                
                #if !UNITY_EDITOR
                if (result == null && _preloadedUnityObjects.TryGetValue(address, out cachedObj))
                {
                    Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Found in cache before return for {address}");
                    result = cachedObj;
                }
                #endif
                
                if (result != null)
                {
                    Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Successfully loaded {result.name} ({result.GetType().Name}) from {address}");
                }
                
                return result;
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "InvalidKeyException")
                {
                    Debug.LogWarning($"[DataConfigManager] LoadUnityObjectByAddress: Exception loading Unity object {address}: {e.Message}\nStackTrace: {e.StackTrace}");
                }
                return null;
            }
            finally
            {
                if (handleCreated && handle.IsValid())
                {
                    try
                    {
                        Addressables.Release(handle);
                        Debug.Log($"[DataConfigManager] LoadUnityObjectByAddress: Handle released for {address}");
                    }
                    catch (Exception releaseEx)
                    {
                        Debug.LogWarning($"[DataConfigManager] LoadUnityObjectByAddress: Exception releasing handle: {releaseEx.Message}");
                    }
                }
            }
        }

        private static bool TryLocateUnityObject(string address, out IList<IResourceLocation> locations)
        {
            locations = null;
            
            try
            {
                if (Addressables.ResourceLocators == null)
                {
                    return false;
                }

                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator != null && locator.Locate(address, typeof(UnityEngine.Object), out locations))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataConfigManager] Error locating Unity object at {address}: {e.Message}");
            }

            return false;
        }

        private static UnityEngine.Object LoadUnityObjectFromResources(string assetPath, string objectTypeName)
        {
            try
            {
                string resourcesPath = assetPath;
                if (resourcesPath.StartsWith("Assets/"))
                {
                    resourcesPath = resourcesPath.Substring(7);
                }
                
                int lastDot = resourcesPath.LastIndexOf('.');
                if (lastDot > 0)
                {
                    resourcesPath = resourcesPath.Substring(0, lastDot);
                }
                
                if (!resourcesPath.Contains("Resources/"))
                {
                    return null;
                }
                
                int resourcesIndex = resourcesPath.IndexOf("Resources/");
                if (resourcesIndex >= 0)
                {
                    resourcesPath = resourcesPath.Substring(resourcesIndex + 10);
                }
                
                Type objectType = Type.GetType(objectTypeName);
                if (objectType != null)
                {
                    return Resources.Load(resourcesPath, objectType);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load Unity object from Resources: {e.Message}");
            }
            
            return null;
        }

        private static void SetFieldValueByPath(object obj, Type type, string fieldPath, object value)
        {
            if (string.IsNullOrEmpty(fieldPath))
                return;

            string[] parts = fieldPath.Split('.');
            object currentObj = obj;
            Type currentType = type;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i];

                if (part.Contains("["))
                {
                    int bracketIndex = part.IndexOf('[');
                    string fieldName = part.Substring(0, bracketIndex);
                    string indexStr = part.Substring(bracketIndex + 1, part.IndexOf(']') - bracketIndex - 1);
                    int index = int.Parse(indexStr);

                    var field = currentType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        Array array = field.GetValue(currentObj) as Array;
                        if (array != null && index < array.Length)
                        {
                            currentObj = array.GetValue(index);
                            currentType = field.FieldType.GetElementType();
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    var field = currentType.GetField(part, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        currentObj = field.GetValue(currentObj);
                        currentType = field.FieldType;
                    }
                    else
                    {
                        return;
                    }
                }

                if (currentObj == null)
                    return;
            }

            string finalPart = parts[^1];

            if (finalPart.Contains("["))
            {
                int bracketIndex = finalPart.IndexOf('[');
                string fieldName = finalPart.Substring(0, bracketIndex);
                string indexStr = finalPart.Substring(bracketIndex + 1, finalPart.IndexOf(']') - bracketIndex - 1);
                int index = int.Parse(indexStr);

                var field = currentType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    Array array = field.GetValue(currentObj) as Array;
                    if (array != null && index < array.Length)
                    {
                        array.SetValue(value, index);
                    }
                }
            }
            else
            {
                var field = currentType.GetField(finalPart, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(currentObj, value);
                }
            }
        }

        #endregion

        #region Serialization helper classes

        [Serializable]
        public class DataConfigWrapper
        {
            [SerializeField] public string typeName;
            [SerializeField] public string jsonData;
            [SerializeField] public string name;
            [SerializeField] public string objectReferencesJson;

            public string TypeName => typeName;
            public string JsonData => jsonData;
            public string Name => name;
            public string ObjectReferencesJson => objectReferencesJson;
        }

        [Serializable]
        public class ObjectReferenceData
        {
            [SerializeField] public string fieldPath;
            [SerializeField] public string objectGuid;
            [SerializeField] public string assetPath;
            [SerializeField] public string objectType;
            [SerializeField] public string address;
        }

        [Serializable]
        public class ConfigObjectReferences
        {
            public List<ObjectReferenceData> references = new List<ObjectReferenceData>();
        }

        #endregion
    }
}

