﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Amethyst.Driver.API;
using Amethyst.Plugins.Contract;
using Amethyst.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Amethyst.Classes;

public class AppTracker : INotifyPropertyChanged
{
    // Is this tracker enabled?
    private bool _isActive;
    private bool _overridePhysics;

    // Internal filters' data
    private Vector3 _kalmanPosition = new(0);

    // LERP data's backup
    private Vector3 _lowPassPosition = new(0);
    private Vector3 _predictedPosition = new(0);
    private Vector3 _lerpPosition = new(0);
    private Vector3 _lastLerpPosition = new(0);

    private Quaternion _slerpOrientation = new(0, 0, 0, 1);
    private Quaternion _slerpSlowOrientation = new(0, 0, 0, 1);
    private Quaternion _lastSlerpOrientation = new(0, 0, 0, 1);
    private Quaternion _lastSlerpSlowOrientation = new(0, 0, 0, 1);

    public Vector3 PoseVelocity { get; set; } = new(0, 0, 0);
    public Vector3 PoseAcceleration { get; set; } = new(0, 0, 0);
    public Vector3 PoseAngularAcceleration { get; set; } = new(0, 0, 0);
    public Vector3 PoseAngularVelocity { get; set; } = new(0, 0, 0);

    // Tracker pose (inherited)
    public Vector3 Position { get; set; } = new(0, 0, 0);
    public Quaternion Orientation { get; set; } = new(1, 0, 0, 0);

    public Vector3 PreviousPosition { get; set; } = new(0, 0, 0);
    public Quaternion PreviousOrientation { get; set; } = new(1, 0, 0, 0);

    public long PoseTimestamp { get; set; } = 0;
    public long PreviousPoseTimestamp { get; set; } = 0;

    // Internal data offset
    public Vector3 PositionOffset = new(0, 0, 0);
    public Vector3 OrientationOffset = new(0, 0, 0);

    // Is this joint overridden?
    public bool IsPositionOverridden { get; set; } = false;
    public bool IsOrientationOverridden { get; set; } = false;

    // Position filter update option
    private RotationTrackingFilterOption _orientationTrackingFilterOption =
        RotationTrackingFilterOption.OrientationTrackingFilterSlerp;

    // Orientation tracking option
    private JointRotationTrackingOption _orientationTrackingOption =
        JointRotationTrackingOption.DeviceInferredRotation;

    // Position filter option
    private JointPositionTrackingOption _positionTrackingFilterOption =
        JointPositionTrackingOption.PositionTrackingFilterLerp;

    public AppTracker()
    {
        InitializeFilters();
    }

    // Does the managing device request no pos filtering?
    private bool _noPositionFilteringRequested = false;

    public bool NoPositionFilteringRequested
    {
        get => _noPositionFilteringRequested;
        set
        {
            // Guard: don't do anything on actual no changes
            if (_noPositionFilteringRequested == value) return;

            _noPositionFilteringRequested = value;
            OnPropertyChanged();
        }
    }

    // Override device's GUID
    public string OverrideGuid { get; set; } = "";

    public TrackerType Role { get; set; } = TrackerType.TrackerHanded;

    // The assigned host joint if using manual joints
    private uint _selectedTrackedJointId = 0;

    public uint SelectedTrackedJointId
    {
        get => _selectedTrackedJointId;
        set
        {
            // Guard: don't do anything on no changes
            if (_selectedTrackedJointId == value) return;

            _selectedTrackedJointId = value;
            OnPropertyChanged(); // All
        }
    }

    // If the joint is overridden, overrides' ids (computed)
    private uint _overrideJointId = 0;

    public uint OverrideJointId
    {
        get => _overrideJointId;
        set
        {
            // Guard: don't do anything on no changes
            if (_overrideJointId == value) return;

            _overrideJointId = value;
            OnPropertyChanged(); // All
        }
    }

    // Tracker data (inherited)
    public string Serial { get; set; } = "";

    public JointRotationTrackingOption OrientationTrackingOption
    {
        get => _orientationTrackingOption;
        set
        {
            // Guard: don't do anything on actual no changes
            if (_orientationTrackingOption == value) return;

            _orientationTrackingOption = value;
            OnPropertyChanged();
        }
    }

    public JointPositionTrackingOption PositionTrackingFilterOption
    {
        get => _positionTrackingFilterOption;
        set
        {
            // Guard: don't do anything on actual no changes
            if (_positionTrackingFilterOption == value) return;

            _positionTrackingFilterOption = value;
            OnPropertyChanged();
        }
    }

    public RotationTrackingFilterOption OrientationTrackingFilterOption
    {
        get => _orientationTrackingFilterOption;
        set
        {
            // Guard: don't do anything on actual no changes
            if (_orientationTrackingFilterOption == value) return;

            _orientationTrackingFilterOption = value;
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            // Don't do anything on no changes
            if (_isActive == value) return;

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public bool OverridePhysics
    {
        get => _overridePhysics;
        set
        {
            // Don't do anything on no changes
            if (_overridePhysics == value) return;

            _overridePhysics = value;
            OnPropertyChanged();
        }
    }

    // Internal position filters
    //private LowPassFilter[] _lowPassFilter = new LowPassFilter[3];
    //private KalmanFilter _kalmanFilter = new KalmanFilter();

    public event PropertyChangedEventHandler PropertyChanged;

    // Get filtered data
    // By default, the saved filter is selected,
    // and to select it, the filter number must be < 0
    public Vector3 GetFilteredPosition(JointPositionTrackingOption? filter = null)
    {
        var computedFilter =
            NoPositionFilteringRequested // If filtering is force-disabled
                ? JointPositionTrackingOption.NoPositionTrackingFilter
                : filter ?? _positionTrackingFilterOption;

        return computedFilter switch
        {
            JointPositionTrackingOption.PositionTrackingFilterLerp => _lerpPosition,
            JointPositionTrackingOption.PositionTrackingFilterLowpass => _lowPassPosition,
            JointPositionTrackingOption.PositionTrackingFilterKalman => _kalmanPosition,
            JointPositionTrackingOption.PositionTrackingFilterPrediction => _predictedPosition,
            JointPositionTrackingOption.NoPositionTrackingFilter => Position,
            _ => Position
        };
    }

    // Get filtered data
    // By default, the saved filter is selected,
    // and to select it, the filter number must be < 0
    public Quaternion GetFilteredOrientation(RotationTrackingFilterOption? filter = null)
    {
        return (filter ?? _orientationTrackingFilterOption) switch
        {
            RotationTrackingFilterOption.OrientationTrackingFilterSlerp => _slerpOrientation,
            RotationTrackingFilterOption.OrientationTrackingFilterSlerpSlow => _slerpSlowOrientation,
            RotationTrackingFilterOption.NoOrientationTrackingFilter => Orientation,
            _ => Orientation
        };
    }

    // Get filtered data
    // By default, the saved filter is selected,
    // and to select it, the filter number must be < 0
    // Additionally, this adds the offsets
    public Vector3 GetFullPosition(JointPositionTrackingOption? filter = null)
    {
        return GetFilteredPosition(filter) + PositionOffset;
    }

    // Get filtered data
    // By default, the saved filter is selected,
    // and to select it, the filter number must be < 0
    // Additionally, this adds the offsets
    public Quaternion GetFullOrientation(RotationTrackingFilterOption? filter = null)
    {
        return GetFilteredOrientation(filter) * Quaternion.CreateFromYawPitchRoll(
            OrientationOffset.Y, OrientationOffset.X, OrientationOffset.Z);
    }

    // Get calibrated position, w/ offsets & filters
    public Vector3 GetFullCalibratedPosition(Quaternion calibrationRotation,
        Vector3 calibrationTranslation, Vector3? calibrationOrigin = null,
        JointPositionTrackingOption? filter = null)
    {
        // Construct the calibrated pose
        return Vector3.Transform(GetFilteredPosition(filter) -
                calibrationOrigin ?? Vector3.Zero, calibrationRotation) +
            calibrationTranslation + calibrationOrigin ?? Vector3.Zero +
            PositionOffset; // Unwrap, rotate, transform, wrap, offset
    }

    // Get calibrated orientation, w/ offsets & filters
    public Quaternion GetFullCalibratedOrientation(Quaternion calibrationRotation,
        RotationTrackingFilterOption? filter = null)
    {
        // Construct the calibrated orientation
        var rawOrientation = GetFilteredOrientation(filter);

        // Construct the calibrated orientation in eigen
        // Note: Apply calibration only in some cases
        if (OrientationTrackingOption != JointRotationTrackingOption.DisableJointRotation &&
            OrientationTrackingOption != JointRotationTrackingOption.FollowHmdRotation)
            rawOrientation = calibrationRotation * rawOrientation;

        // Return the calibrated orientation with offset
        return Quaternion.CreateFromYawPitchRoll(OrientationOffset.Y,
            OrientationOffset.X, OrientationOffset.Z) * rawOrientation;
    }

    // Get filtered data
    // By default, the saved filter is selected,
    // and to select it, the filter number must be < 0
    // Additionally, this adds the offsets
    // Offset will be added after translation
    public Vector3 GetCalibratedVector(Vector3 positionVector,
        Quaternion calibrationRotation, Vector3 calibrationTranslation,
        Vector3? calibrationOrigin = null)
    {
        // Construct the calibrated pose
        return Vector3.Transform(positionVector -
                calibrationOrigin ?? Vector3.Zero, calibrationRotation) +
            calibrationTranslation + calibrationOrigin ?? Vector3.Zero +
            PositionOffset; // Unwrap, rotate, transform, wrap, offset
    }

    // Get tracker base
    // This is for updating the server with
    // exclusive filtered data from K2AppTracker
    // By default, the saved filter is selected
    // Offsets are added inside called methods
    public K2TrackerBase GetTrackerBase(Quaternion calibrationRotation,
        Vector3 calibrationTranslation, Vector3 calibrationOrigin,
        JointPositionTrackingOption? posFilter = null,
        RotationTrackingFilterOption? oriFilter = null)
    {
        // Check if matrices are empty
        var notCalibrated = calibrationRotation.IsIdentity &&
                            calibrationTranslation.Equals(Vector3.Zero) &&
                            calibrationOrigin.Equals(Vector3.Zero);

        // Construct the return type
        var trackerBase = new K2TrackerBase()
        {
            Data = new K2TrackerData { IsActive = IsActive, Role = Role, Serial = Serial },
            Tracker = Role
        };

        var fullOrientation = notCalibrated
            ? GetFullOrientation(oriFilter)
            : GetFullCalibratedOrientation(
                calibrationRotation, oriFilter);

        var fullPosition = notCalibrated
            ? GetFullPosition(posFilter)
            : GetFullCalibratedPosition(calibrationRotation,
                calibrationTranslation, calibrationOrigin, posFilter);

        trackerBase.Pose.Orientation = new K2Quaternion
        {
            W = fullOrientation.W, X = fullOrientation.X, Y = fullOrientation.Y, Z = fullOrientation.Z
        };

        trackerBase.Pose.Position = new K2Vector3
        {
            X = fullPosition.X, Y = fullPosition.Y, Z = fullPosition.Z
        };

        if (!OverridePhysics) return trackerBase;

        var fullVelocity = notCalibrated
            ? PoseVelocity
            : GetCalibratedVector(
                PoseVelocity, calibrationRotation,
                calibrationTranslation, calibrationOrigin);

        var fullAcceleration = notCalibrated
            ? PoseAcceleration
            : GetCalibratedVector(
                PoseAcceleration, calibrationRotation,
                calibrationTranslation, calibrationOrigin);

        var fullAngularVelocity = notCalibrated
            ? PoseAngularVelocity
            : GetCalibratedVector(
                PoseAngularVelocity, calibrationRotation,
                calibrationTranslation, calibrationOrigin);

        var fullAngularAcceleration = notCalibrated
            ? PoseAngularAcceleration
            : GetCalibratedVector(
                PoseAngularAcceleration, calibrationRotation,
                calibrationTranslation, calibrationOrigin);

        trackerBase.Pose.Physics.Velocity = new K2Vector3
        {
            X = fullVelocity.X,
            Y = fullVelocity.Y,
            Z = fullVelocity.Z
        };

        trackerBase.Pose.Physics.Acceleration = new K2Vector3
        {
            X = fullAcceleration.X,
            Y = fullAcceleration.Y,
            Z = fullAcceleration.Z
        };

        trackerBase.Pose.Physics.AngularVelocity = new K2Vector3
        {
            X = fullAngularVelocity.X,
            Y = fullAngularVelocity.Y,
            Z = fullAngularVelocity.Z
        };

        trackerBase.Pose.Physics.AngularAcceleration = new K2Vector3
        {
            X = fullAngularAcceleration.X,
            Y = fullAngularAcceleration.Y,
            Z = fullAngularAcceleration.Z
        };

        return trackerBase;
    }

    // Get tracker base
    // This is for updating the server with
    // exclusive filtered data from K2AppTracker
    // By default, the saved filter is selected
    // Offsets are added inside called methods
    public K2TrackerBase GetTrackerBase(
        JointPositionTrackingOption? posFilter = null,
        RotationTrackingFilterOption? oriFilter = null)
    {
        // Construct the return type
        var trackerBase = new K2TrackerBase()
        {
            Data = new K2TrackerData { IsActive = IsActive, Role = Role, Serial = Serial },
            Tracker = Role
        };

        var fullOrientation = GetFullOrientation(oriFilter);
        var fullPosition = GetFullPosition(posFilter);

        trackerBase.Pose.Orientation = new K2Quaternion
        {
            W = fullOrientation.W,
            X = fullOrientation.X,
            Y = fullOrientation.Y,
            Z = fullOrientation.Z
        };

        trackerBase.Pose.Position = new K2Vector3
        {
            X = fullPosition.X,
            Y = fullPosition.Y,
            Z = fullPosition.Z
        };

        if (!OverridePhysics) return trackerBase;

        trackerBase.Pose.Physics.Velocity = new K2Vector3
        {
            X = PoseVelocity.X,
            Y = PoseVelocity.Y,
            Z = PoseVelocity.Z
        };

        trackerBase.Pose.Physics.Acceleration = new K2Vector3
        {
            X = PoseAcceleration.X,
            Y = PoseAcceleration.Y,
            Z = PoseAcceleration.Z
        };

        trackerBase.Pose.Physics.AngularVelocity = new K2Vector3
        {
            X = PoseAngularVelocity.X,
            Y = PoseAngularVelocity.Y,
            Z = PoseAngularVelocity.Z
        };

        trackerBase.Pose.Physics.AngularAcceleration = new K2Vector3
        {
            X = PoseAngularAcceleration.X,
            Y = PoseAngularAcceleration.Y,
            Z = PoseAngularAcceleration.Z
        };

        return trackerBase;
    }

    public void InitializeFilters()
    {
        // Low pass filter initialization

        // Kalman filter initialization
    }

    public void UpdateFilters()
    {
        // Low pass filter initialization

        // Kalman filter initialization
    }

    public TrackedJoint GetTrackedJoint()
    {
        return new TrackedJoint(Serial)
        {
            JointAcceleration = PoseAcceleration,
            JointAngularAcceleration = PoseAngularAcceleration,
            JointAngularVelocity = PoseAngularVelocity,
            JointOrientation = Orientation,
            JointPosition = Position,
            JointVelocity = PoseVelocity,
            PreviousJointOrientation = PreviousOrientation,
            PreviousJointPosition = PreviousPosition,
            TrackingState = IsActive
                ? TrackedJointState.StateTracked
                : TrackedJointState.StateNotTracked
        };
    }

    public void OnPropertyChanged(string propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        PropertyChangedEvent?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    // OnPropertyChanged listener for containers
    public EventHandler PropertyChangedEvent;

    // MVVM stuff
    public string GetResourceString(string key)
    {
        return Interfacing.LocalizedJsonString(key);
    }

    public bool InvertBool(bool v)
    {
        return !v;
    }

    public string TrackerName => Interfacing.LocalizedJsonString($"/SharedStrings/Joints/Enum/{(int)Role}");

    public int PositionTrackingDisplayOption
    {
        get => NoPositionFilteringRequested ? -1 : (int)PositionTrackingFilterOption;
        set
        {
            PositionTrackingFilterOption = (JointPositionTrackingOption)value;
            OnPropertyChanged("PositionTrackingDisplayOption");
        }
    }

    public int OrientationTrackingDisplayOption
    {
        get => (int)OrientationTrackingOption;
        set
        {
            OrientationTrackingOption = (JointRotationTrackingOption)value;
            OnPropertyChanged("OrientationTrackingDisplayOption");
        }
    }

    public bool AppOrientationSupported =>
        Role is TrackerType.TrackerLeftFoot or TrackerType.TrackerRightFoot &&
        TrackingDevices.GetTrackingDevice().IsAppOrientationSupported;

    public string GetManagingDeviceGuid =>
        IsPositionOverridden || IsOrientationOverridden ? OverrideGuid : AppData.Settings.TrackingDeviceGuid;

    public string ManagingDevicePlaceholder =>
        GetResourceString("/SettingsPage/Filters/Managed").Replace("{0}", GetManagingDeviceGuid);

    private bool _isTrackerExpanderOpen = false;

    public bool IsTrackerExpanderOpen
    {
        get => _isTrackerExpanderOpen && IsActive;
        set
        {
            _isTrackerExpanderOpen = value;
            OnPropertyChanged("IsTrackerExpanderOpen");
        }
    }

    // The assigned host joint if using manual joints
    public int SelectedBaseTrackedJointId
    {
        get => IsActive ? (int)_selectedTrackedJointId : -1;
        set
        {
            _selectedTrackedJointId = value >= 0 ? (uint)value : 0;
            OnPropertyChanged(); // All
        }
    }

    public int SelectedOverrideJointId
    {
        // '+ 1' and '- 1' cause '0' is 'No Override' in this case
        // Note: use OverrideJointId for the "normal" (non-ui) one
        get => IsActive ? (int)_overrideJointId + 1 : -1;
        set
        {
            _overrideJointId = value > 0 ? (uint)(value - 1) : 0;
            OnPropertyChanged(); // All
        }
    }

    public int SelectedOverrideJointIdForSelectedDevice
    {
        // '+ 1' and '- 1' cause '0' is 'No Override' in this case
        // Note: use OverrideJointId for the "normal" (non-ui) one
        get => IsActive ? IsManagedBy(Shared.Devices.SelectedTrackingDeviceGuid) ? (int)_overrideJointId + 1 : 0 : -1;
        set
        {
            // Update the override joint and the managing device
            _overrideJointId = value > 0 ? (uint)(value - 1) : 0;
            OverrideGuid = Shared.Devices.SelectedTrackingDeviceGuid;

            // Enable at least 1 override
            if (!IsOverriden)
                IsPositionOverridden = true;

            OnPropertyChanged(); // All
        }
    }

    // Is force-updated by the base device
    public bool IsAutoManaged => TrackingDevices.GetTrackingDevice().TrackedJoints.Any(x =>
        x.Role != TrackedJointType.JointManual && TypeUtils.JointTrackerTypeDictionary[x.Role] == Role);

    // Is NOT force-updated by the base device
    public bool IsManuallyManaged => !IsAutoManaged;

    // IsPositionOverridden || IsOrientationOverridden
    public bool IsOverriden => IsPositionOverridden || IsOrientationOverridden;

    public double BoolToOpacity(bool v)
    {
        return v ? 1.0 : 0.0;
    }

    // Is this joint overridden by the selected device? (pos)
    public bool IsPositionOverriddenBySelectedDevice
    {
        get => OverrideGuid == Shared.Devices.SelectedTrackingDeviceGuid && IsPositionOverridden;
        set
        {
            // Update the managing and the override
            OverrideGuid = Shared.Devices.SelectedTrackingDeviceGuid;
            IsPositionOverridden = value;

            // If not overridden yet (index=0)
            if (_overrideJointId <= 0)
                _overrideJointId = 1;

            OnPropertyChanged(); // All

        }
    }

    // Is this joint overridden by the selected device? (ori)
    public bool IsOrientationOverriddenBySelectedDevice
    {
        get => OverrideGuid == Shared.Devices.SelectedTrackingDeviceGuid && IsOrientationOverridden;
        set
        {
            // Update the managing and the override
            OverrideGuid = Shared.Devices.SelectedTrackingDeviceGuid;
            IsOrientationOverridden = value;

            // If not overridden yet (index=0)
            if (_overrideJointId <= 0)
                _overrideJointId = 1;

            OnPropertyChanged(); // All

        }
    }
    
    public bool IsOverridenByOtherDevice => OverrideGuid == Shared.Devices.SelectedTrackingDeviceGuid;

    public bool IsManagedBy(string guid)
    {
        return guid == GetManagingDeviceGuid;
    }
}