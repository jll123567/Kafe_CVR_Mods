﻿using ABI_RC.Core.Util;
using ABI.CCK.Components;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Components.MenuHandlers;

public static class SpawnableMenuHandler {

    static SpawnableMenuHandler() {
        Events.DebuggerMenu.ControlsNextPage += () => {
            _pageIncrement += 1;
        };
        Events.DebuggerMenu.ControlsPreviousPage += () => {
            _pageIncrement -= 1;
        };

        SpawnablesPropData = new List<CVRSyncHelper.PropData>();
        SyncedParametersValues = new Dictionary<CVRSpawnableValue, TextMeshProUGUI>();
        AttachmentsValues = new Dictionary<CVRAttachment, TextMeshProUGUI>();

        SyncTypeDict = new() {
            { 1, "GrabbedByMe" },
            { 3, "TeleGrabbed" },
            { 2, "Attached" },
        };
    }

    private static readonly Dictionary<int, string> SyncTypeDict;

    private static readonly List<CVRSyncHelper.PropData> SpawnablesPropData;

    // Current Spawnable Prop Data
    private static int _currentSpawnablePropDataIndex;
    private static CVRSyncHelper.PropData _currentSpawnablePropData;

    private static int _pageIncrement;
    private static bool _spawnableChanged;

    private static void UpdateCurrentProp() {

        // Update indexes
        SpawnablesPropData.Clear();
        for (var index = 0; index < CVRSyncHelper.Props.Count; index++) {
            var propData = CVRSyncHelper.Props[index];

            // Ignore spawnables if they're null
            if (propData == null || propData.Spawnable == null) continue;

            SpawnablesPropData.Add(propData);
        }

        _currentSpawnablePropDataIndex = SpawnablesPropData.IndexOf(_currentSpawnablePropData);

        // If there is no match, reset the spawnable and return
        if (_currentSpawnablePropDataIndex == -1) {
            _currentSpawnablePropData = null;
            _currentSpawnablePropDataIndex = 0;

            if (SpawnablesPropData.Count > 0) {
                _currentSpawnablePropData = SpawnablesPropData[_currentSpawnablePropDataIndex];
            }

            _spawnableChanged = true;
            return;
        }

        // Otherwise update the index with the page increment count
        if (_pageIncrement != 0) {
            _currentSpawnablePropDataIndex = (_currentSpawnablePropDataIndex + SpawnablesPropData.Count + _pageIncrement) % SpawnablesPropData.Count;
            _pageIncrement = 0;
            _currentSpawnablePropData = SpawnablesPropData[_currentSpawnablePropDataIndex];
            _spawnableChanged = true;
        }
    }

    // Attributes
    private static TextMeshProUGUI _attributeId;
    private static TextMeshProUGUI _attributeSpawnedByValue;
    private static TextMeshProUGUI _attributeSyncedByValue;
    private static TextMeshProUGUI _attributeSyncTypeValue;

    // Parameters
    private static GameObject _categorySyncedParameters;
    private static readonly Dictionary<CVRSpawnableValue, TextMeshProUGUI> SyncedParametersValues;

    // Main animator Parameters
    private static GameObject _categoryMainAnimatorParameters;
    private static Animator _mainAnimator;

    // Pickups
    private static GameObject _categoryPickups;
    private static readonly Dictionary<CVRPickupObject, TextMeshProUGUI> PickupsValues = new();

    // Attachments
    private static GameObject _categoryAttachments;
    private static readonly Dictionary<CVRAttachment, TextMeshProUGUI> AttachmentsValues;


    public static void Init(Menu menu) {

        menu.AddNewDebugger("Props");

        var categoryAttributes = menu.AddCategory("Attributes");
        _attributeId = menu.AddCategoryEntry(categoryAttributes, "Name/ID:");
        _attributeSpawnedByValue = menu.AddCategoryEntry(categoryAttributes, "Spawned By:");
        _attributeSyncedByValue = menu.AddCategoryEntry(categoryAttributes, "Synced By:");
        _attributeSyncTypeValue = menu.AddCategoryEntry(categoryAttributes, "Sync Type:");

        _categorySyncedParameters = menu.AddCategory("Synced Parameters");

        _categoryMainAnimatorParameters = menu.AddCategory("Main Animator Parameters");

        _categoryPickups = menu.AddCategory("Pickups");

        _categoryAttachments = menu.AddCategory("Attachments");
    }

    public static void Update(Menu menu) {
        UpdateCurrentProp();

        var propCount = SpawnablesPropData.Count;

        menu.SetControlsExtra($"({_currentSpawnablePropDataIndex+1}/{propCount})");

        menu.ToggleCategories(propCount > 0);

        menu.ShowControls(propCount > 1);

        if (propCount < 1) return;

        var currentSpawnable = _currentSpawnablePropData.Spawnable;

        // Prop Data Info
        _attributeId.SetText(menu.GetSpawnableName(_currentSpawnablePropData?.ObjectId));
        _attributeSpawnedByValue.SetText(menu.GetUsername(_currentSpawnablePropData?.SpawnedBy));
        _attributeSyncedByValue.SetText(menu.GetUsername(_currentSpawnablePropData?.syncedBy));
        var syncType = _currentSpawnablePropData?.syncType;
        string syncTypeString = "N/A";
        string syncTypeValue = "N/A";
        if (syncType.HasValue) {
            syncTypeValue = syncType.Value.ToString();
            if (SyncTypeDict.ContainsKey(syncType.Value)) {
                syncTypeString = SyncTypeDict[syncType.Value];
            }
            else {
                syncTypeString = currentSpawnable.isPhysicsSynced ? "Physics" : "None";
            }
        }
        _attributeSyncTypeValue.SetText($"{syncTypeValue} [{syncTypeString}?]");

        // Update the menus if the spawnable changed
        if (_spawnableChanged) {

            // Place the highlighter on the first collider found
            var firstCollider = currentSpawnable.transform.GetComponentInChildren<Collider>();
            Highlighter.SetTargetHighlight(firstCollider.gameObject);

            // Restore parameters
            menu.ClearCategory(_categorySyncedParameters);
            SyncedParametersValues.Clear();
            foreach (var syncValue in currentSpawnable.syncValues) {
                var tmpParamValue = menu.AddCategoryEntry(_categorySyncedParameters, syncValue.name);
                SyncedParametersValues[syncValue] = tmpParamValue;
            }

            // Restore Main Animator Parameters
            menu.ClearCategory(_categoryMainAnimatorParameters);
            ParameterEntry.Entries.Clear();
            _mainAnimator = currentSpawnable.gameObject.GetComponent<Animator>();
            if (_mainAnimator != null) {
                foreach (var parameter in _mainAnimator.parameters) {
                    var tmpPickupValue = menu.AddCategoryEntry(_categoryMainAnimatorParameters, parameter.name);
                    ParameterEntry.Add(_mainAnimator, parameter, tmpPickupValue);
                }
            }

            // Restore Pickups
            menu.ClearCategory(_categoryPickups);
            PickupsValues.Clear();
            var pickups = Traverse.Create(currentSpawnable).Field("pickups").GetValue<CVRPickupObject[]>();
            foreach (var pickup in pickups) {
                var tmpPickupValue = menu.AddCategoryEntry(_categoryPickups, pickup.name);
                PickupsValues[pickup] = tmpPickupValue;
            }

            // Restore Attachments
            menu.ClearCategory(_categoryAttachments);
            AttachmentsValues.Clear();
            var attachments = Traverse.Create(currentSpawnable).Field("_attachments").GetValue<List<CVRAttachment>>();
            foreach (var attachment in attachments) {
                var tmpPickupValue = menu.AddCategoryEntry(_categoryAttachments, attachment.name);
                AttachmentsValues[attachment] = tmpPickupValue;
            }

            // Consume the spawnable changed
            _spawnableChanged = false;
        }

        // Update sync parameter values
        foreach (var syncedParametersValue in SyncedParametersValues) {
            syncedParametersValue.Value.SetText(syncedParametersValue.Key.currentValue.ToString());
        }

        // Update main animator parameter values
        if (_mainAnimator != null) {
            foreach (var entry in ParameterEntry.Entries) entry.Update();
        }

        // Update pickup values
        foreach (var pickupValue in PickupsValues) {
            pickupValue.Value.SetText($" GrabbedBy: {menu.GetUsername(pickupValue.Key.grabbedBy)}");
        }

        // Update attachment values
        foreach (var attachmentsValue in AttachmentsValues) {
            var attachedTransformName = "";
            if (attachmentsValue.Key.IsAttached()) {
                var attTrns = Traverse.Create(attachmentsValue.Key).Field("_attachedTransform").GetValue<Transform>();
                if (attTrns != null) {
                    attachedTransformName = $" [{attTrns.gameObject.name}]";
                }
            }
            var attachedStr = attachmentsValue.Key.IsAttached() ? "yes" : "no";
            attachmentsValue.Value.SetText($"Attached: {attachedStr}{attachedTransformName}");
        }
    }
}