extern alias protoNet;
using System.Reflection;
using Contoso.Protobuf;
using Google.Protobuf;
using Protobuf.DynamicJson.Converter;
using Protobuf.DynamicJson.Descriptors;
using Test;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Protobuf.DynamicJson.Tests;

public sealed class ProtobufJsonConverterTests
{
    [Theory]
    [MemberData(nameof(ProtoSpecs))]
    public void ConvertJsonToProtoBytes_WithValidProtoSpecs_ProducesEquivalentMessages(string messageName, string protoSpec, string sampleProto)
    {
        var desc = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSpec);
        var bytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc);

        AssertParsedMessagesAreEqual(messageName, sampleProto, bytes);
    }

    [Theory]
    [MemberData(nameof(ProtoSpecs))]
    public void ConvertProtoBytesToJson_RoundTrip_PreservesMessage(string messageName, string protoSpec, string sampleProto)
    {
        var desc = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSpec);
        var originalBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc);
        var jsonFromBytes = ProtobufJsonConverter.ConvertProtoBytesToJson(originalBytes, messageName, desc);
        var roundTrippedBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(jsonFromBytes, messageName, desc);

        AssertParsedMessagesAreEqual(messageName, sampleProto, roundTrippedBytes);
    }

    [Fact]
    public void ConvertJsonToProtoBytes_ShouldReuseDescriptorCache_ForEquivalentDescriptorBytes()
    {
        // Arrange
        const string protoSpec = """
                                     syntax = "proto3";
                                     message MyMessage {
                                         int32 myField = 1;
                                     }
                                 """;

        var json = """{ "myField": 123 }""";
        var messageName = "MyMessage";

        var descriptorBytes1 = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSpec);
        var descriptorBytes2 = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSpec); // New instance with same content

        // Clear cache (via reflection)
        var cacheField = typeof(ProtobufJsonConverter)
            .GetField("descriptorCache", BindingFlags.Static | BindingFlags.NonPublic);
        var cache = cacheField?.GetValue(null) as Microsoft.Extensions.Caching.Memory.MemoryCache;
        cache?.Compact(1.0); // 1.0 means remove 100% of entries

        // Act - First conversion (will populate the cache)
        var result1 = ProtobufJsonConverter.ConvertJsonToProtoBytes(json, messageName, descriptorBytes1);

        // Act - Second conversion (should hit the cache)
        var result2 = ProtobufJsonConverter.ConvertJsonToProtoBytes(json, messageName, descriptorBytes2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, cache?.Count);
    }
    
    [Fact]
    public void ConvertJsonToProtoBytes_ShouldNotReuseDescriptorCache_ForDifferentDescriptorBytes()
    {
        // Arrange
        const string protoSpec1 = """
                                      syntax = "proto3";
                                      message MyMessage {
                                          int32 myField = 1;
                                      }
                                  """;

        const string protoSpec2 = """
                                      syntax = "proto3";
                                      message MyMessage2 {
                                          int32 myField2 = 1;
                                      }
                                  """;

        var json1 = """{ "myField": 123 }""";
        var messageName1 = "MyMessage";

        var json2 = """{ "myField2": 123 }""";
        var messageName2 = "MyMessage2";

        var descriptorBytes1 = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSpec1);
        var descriptorBytes2 = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSpec2);

        // Clear cache (via reflection)
        var cacheField = typeof(ProtobufJsonConverter)
            .GetField("descriptorCache", BindingFlags.Static | BindingFlags.NonPublic);
        var cache = cacheField?.GetValue(null) as Microsoft.Extensions.Caching.Memory.MemoryCache;
        cache?.Compact(1.0); // 1.0 means remove 100% of entries

        // Act
        var result1 = ProtobufJsonConverter.ConvertJsonToProtoBytes(json1, messageName1, descriptorBytes1);
        var result2 = ProtobufJsonConverter.ConvertJsonToProtoBytes(json2, messageName2, descriptorBytes2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, cache?.Count);
    }
    
    public static TheoryData<string, string, string> ProtoSpecs =>
        new()
        {
            {
                "contoso.protobuf.configuration.DeviceConfiguration",
                "syntax = \"proto3\";\n\npackage contoso.protobuf.configuration;\n\noption csharp_namespace = \"Contoso.Protobuf\";\noption go_package = \"git.contoso.com/dev/GatewayGoProto-golang/configuration\";\n\nmessage DeviceConfiguration {\n  MasterSwitches master_switches = 1;\n  DeviceParameters device_parameters = 2;\n}\n\nmessage MasterSwitches\n{\n    bytes flags = 1;\n}\n\nenum ParameterKey {\n    PARAMETER_KEY_MinAccSpeed = 0;\n    PARAMETER_KEY_MinLogTime = 1;\n    PARAMETER_KEY_DeltaSpeed = 2;\n    PARAMETER_KEY_DeltaSpeedMinSpeed = 3;\n    PARAMETER_KEY_DeltaHeading = 4;\n    PARAMETER_KEY_DeltaHeadingMinSpeed = 5;\n    PARAMETER_KEY_HiDeltaHead = 6;\n    PARAMETER_KEY_HiDeltaHeadMinSpeed = 7;\n    PARAMETER_KEY_SpeedingSpeed = 8;\n    PARAMETER_KEY_SpeedingResetSpeed = 9;\n    PARAMETER_KEY_Options = 10;\n    PARAMETER_KEY_NfcGracePeriod = 11;\n    PARAMETER_KEY_GpsShutdownDelay = 12;\n    PARAMETER_KEY_MaxInvalid = 13;\n    PARAMETER_KEY_NoMsgDelay = 14;\n    PARAMETER_KEY_Version = 15;\n    PARAMETER_KEY_HarshBrake = 16;\n    PARAMETER_KEY_CommParam = 17;\n    PARAMETER_KEY_CompanyId = 18;\n    PARAMETER_KEY_Aux1Speed = 19;\n    PARAMETER_KEY_Aux2Speed = 20;\n    PARAMETER_KEY_Aux3Speed = 21;\n    PARAMETER_KEY_Aux4Speed = 22;\n    PARAMETER_KEY_Aux5Speed = 23;\n    PARAMETER_KEY_Aux6Speed = 24;\n    PARAMETER_KEY_Aux7Speed = 25;\n    PARAMETER_KEY_Aux8Speed = 26;\n    PARAMETER_KEY_AuxWarningCompare = 27;\n    PARAMETER_KEY_AuxSaveTotalFuelOnChange = 28;\n    PARAMETER_KEY_MoreOptions2 = 29;\n    PARAMETER_KEY_AuxRecordRpmOnChange = 30;\n    PARAMETER_KEY_AuxRecordRpmWhileActive = 31;\n    PARAMETER_KEY_MiscRecordRpm = 32;\n    PARAMETER_KEY_RpmDelayAfterAuxChange = 33;\n    PARAMETER_KEY_RpmDelayBetweenLogsWhileAuxActive = 34;\n    PARAMETER_KEY_RpmDelayAfterMiscChange = 35;\n    PARAMETER_KEY_IdleWarningTime = 36;\n    PARAMETER_KEY_AuxConfig = 37;\n    PARAMETER_KEY_SeatbeltAsAux = 38;\n    PARAMETER_KEY_OccupancyAsAux = 39;\n    PARAMETER_KEY_PassengerViolationAsAux = 40;\n    PARAMETER_KEY_OverRevThreshold = 41;\n    PARAMETER_KEY_LowVoltage = 42;\n    PARAMETER_KEY_MoreOptions3 = 43;\n    PARAMETER_KEY_ExternalDeviceShutdownDelay = 44;\n    PARAMETER_KEY_IridiumFlags = 45;\n    PARAMETER_KEY_IridiumUpdatePeriod = 46;\n    PARAMETER_KEY_IridiumAuxMonitorMask = 47;\n    PARAMETER_KEY_IridiumAuxEmergencyMask = 48;\n    PARAMETER_KEY_SeatbeltWarningSpeed = 49;\n    PARAMETER_KEY_AdvancedOptions = 50;\n    PARAMETER_KEY_AccelLowThreshold = 51;\n    PARAMETER_KEY_AccelHighThreshold = 52;\n    PARAMETER_KEY_AuxDebounceMask = 53;\n    PARAMETER_KEY_MaxPositionEstimateError = 54;\n    PARAMETER_KEY_MaxCurveDistanceError = 55;\n    PARAMETER_KEY_MaxSpeedEstimateError = 56;\n    PARAMETER_KEY_DisableAuxMask = 57;\n    PARAMETER_KEY_MaxCurveSpeedError = 58;\n    PARAMETER_KEY_AccelAccidentMagnitudeThreshold = 59;\n    PARAMETER_KEY_MoreOptions = 60;\n    PARAMETER_KEY_AccelAccelerationThreshold = 61;\n    PARAMETER_KEY_AccelBrakingThreshold = 62;\n    PARAMETER_KEY_AccelCorneringThreshold = 63;\n    PARAMETER_KEY_MinTimeForVoltageWarning = 64;\n    PARAMETER_KEY_DisabledDrivers = 65;\n    PARAMETER_KEY_AccelThresholdFactor = 66;\n    PARAMETER_KEY_LedBrightnessGps = 67;\n    PARAMETER_KEY_LedBrightnessIgn = 68;\n    PARAMETER_KEY_LedBrightnessModem = 69;\n    PARAMETER_KEY_SpeedingAlertCount = 70;\n    PARAMETER_KEY_EngineSize = 71;\n    PARAMETER_KEY_FuelType = 72;\n    PARAMETER_KEY_FuelScaling = 73;\n    PARAMETER_KEY_AccelRolloverThreshold = 74;\n    PARAMETER_KEY_AdvancedOptions2 = 75;\n    PARAMETER_KEY_CurveHiResRpmAllowedError = 76;\n    PARAMETER_KEY_TTS_LanguageID = 77;\n    PARAMETER_KEY_NumOfValidGpsToIgnore = 78;\n    PARAMETER_KEY_AuxDebounceDelayMask = 79;\n    PARAMETER_KEY_GpsSaveEveryNumSamples = 80;\n    PARAMETER_KEY_TrailerTrackingLogStoppedTime = 81;\n    PARAMETER_KEY_TrailerTrackingLogDrivingTime = 82;\n    PARAMETER_KEY_TTS_minVolume = 83;\n    PARAMETER_KEY_CorneringWarningSpeed = 84;\n    PARAMETER_KEY_NFCRelay_AuxLockoutMask = 85;\n    PARAMETER_KEY_NFCRelay_AuxLockoutPattern = 86;\n    PARAMETER_KEY_AdvancedOptions3 = 87;\n    PARAMETER_KEY_AnalogPullUps = 88;\n    PARAMETER_KEY_EnableAuxAsOnceOffFault = 89;\n    PARAMETER_KEY_AuxOnceOffPolarity = 90;\n    PARAMETER_KEY_AlternateSourceFromServer = 91;\n    PARAMETER_KEY_AirplaneModePerimeter = 92;\n    PARAMETER_KEY_BusIdleWaitTimeJ1708 = 93;\n    PARAMETER_KEY_SecondsSpeedingBeforeAlert = 94;\n    PARAMETER_KEY_SocValueBasedLoggingThreshold = 95;\n    PARAMETER_KEY_MoreOptions4 = 96;\n    PARAMETER_KEY_VehicleDiagnosticAlertControl = 97;\n    PARAMETER_KEY_MoreOptions5 = 98;\n    PARAMETER_KEY_DataCaptureTriggerLimitPerHour = 99;\n    PARAMETER_KEY_AccelVerbosePeriod = 100;\n    PARAMETER_KEY_DataCaptureTrigger1 = 101;\n    PARAMETER_KEY_DataCaptureTrigger2 = 102;\n    PARAMETER_KEY_DataCaptureTrigger3 = 103;\n    PARAMETER_KEY_UnknownDriverAlarmDurationSecs = 104;\n    PARAMETER_KEY_ReservedDummy = 105;\n    PARAMETER_KEY_ccLowVoltage = 106;\n    PARAMETER_KEY_CustomNrc78P2Timeout = 107;\n    PARAMETER_KEY_CustomNrc78TimeoutSource = 108;\n    PARAMETER_KEY_ExclusiveObd2SourceToUseWithPhysicalRqs = 109;\n    PARAMETER_KEY_InterfaceOnWhichFuncRqSentAsPhysical = 110;\n    PARAMETER_KEY_StationaryHoldDecreaseSpeed = 111;\n    PARAMETER_KEY_AccelThrottlePositionThreshold = 112;\n    PARAMETER_KEY_DurationOfBeepingWhenSpeeding = 113;\n    PARAMETER_KEY_DurationOfBeepingWhenIdling = 114;\n    PARAMETER_KEY_ForcedHighestPriorityFuelSource = 115;\n    PARAMETER_KEY_ForcedHighestPriorityIdleFuelSource = 116;\n    PARAMETER_KEY_ForcedHighestPriorityFuelDivisor = 117;\n    PARAMETER_KEY_PreReserved1 = 118;\n    PARAMETER_KEY_PreReserved2 = 119;\n    PARAMETER_KEY_PreReserved3 = 120;\n    PARAMETER_KEY_END_OF_LEGACY = 121;\n    PARAMETER_KEY_MovementCommunicationInterval = 122;\n    PARAMETER_KEY_CarbDataCollectionCycle = 123;\n    PARAMETER_KEY_BistStartupDelay = 124;\n    PARAMETER_KEY_MinBatteryVoltage = 125;\n    PARAMETER_KEY_MaxTemperature = 126;\n    PARAMETER_KEY_ShippingNetworkTimeout = 127;\n    PARAMETER_KEY_ShippingGwTimeout = 128;\n    PARAMETER_KEY_ShippingHealthCheckInterval = 129;\n    PARAMETER_KEY_ShippingCommunicationInterval = 130;\n    PARAMETER_KEY_SleepCommunicationInterval = 131;\n    PARAMETER_KEY_SleepHealthCheckInterval = 132;\n    PARAMETER_KEY_SleepHealthCheckIntervalReduced = 133;\n    PARAMETER_KEY_SleepNetworkTimeout = 134;\n    PARAMETER_KEY_SleepGwTimeout = 135;\n    PARAMETER_KEY_MovementHealthCheckInterval = 136;\n    PARAMETER_KEY_GnssFixInterval = 137;\n    PARAMETER_KEY_GnssFixCount = 138;\n    PARAMETER_KEY_GnssMinDistance = 139;\n    PARAMETER_KEY_SpeedThreshold = 140;\n    PARAMETER_KEY_SpeedDuration = 141;\n    PARAMETER_KEY_AccelerationThreshold = 142;\n    PARAMETER_KEY_SpeedDetectionMin = 143;\n    PARAMETER_KEY_CommunicationRetryMax = 144;\n    PARAMETER_KEY_ModemMaxConnectionTime = 145;\n    PARAMETER_KEY_GsmRssiMin = 146;\n    PARAMETER_KEY_GsmSinrMin = 147;\n    PARAMETER_KEY_LteRssiMin = 148;\n    PARAMETER_KEY_LteSinrMin = 149;\n    PARAMETER_KEY_ModemMinVoltage = 150;\n    PARAMETER_KEY_IoxWrksSpreaderConfiguration = 151;\n    PARAMETER_KEY_IoxWrksIoConfiguration = 152;\n    PARAMETER_KEY_MovementDetectionDistanceThreshold = 153;\n    PARAMETER_KEY_AssetRecoveryModeEnabled = 154;\n    PARAMETER_KEY_AssetRecoveryInterval = 155;\n    PARAMETER_KEY_CheckInOnTripEnabled = 156;\n    PARAMETER_KEY_PowerBudget = 157;\n    PARAMETER_KEY_PowerBudgetPeriodDays = 158;\n    PARAMETER_KEY_TimedCheckInEnabled = 159;\n    PARAMETER_KEY_TimedCheckInEntries = 160;\n    PARAMETER_KEY_TimedCheckIn = 161;\n    PARAMETER_KEY_TimedCheckInRetryInterval = 162;\n    PARAMETER_KEY_ScheduledSyncEnabled = 163;\n    PARAMETER_KEY_ScheduledSyncTime = 164;\n    PARAMETER_KEY_CheckInOnTripEndEnabled = 165;\n    PARAMETER_KEY_DisableAllAccelLogging = 166;\n    PARAMETER_KEY_DisableAllAdasLogging = 167;\n    PARAMETER_KEY_CheckInOnTripGnssInterval = 168;\n    PARAMETER_KEY_CheckInOnTripNumGnssFixes = 169;\n    PARAMETER_KEY_TimeBasedModeEnabled = 170;\n    PARAMETER_KEY_TimeZoneOverride = 171;\n    PARAMETER_KEY_TimeZoneOffset = 172;\n    PARAMETER_KEY_CarbVehicleStationaryTimeInSeconds = 173;\n    PARAMETER_KEY_LteRsrpMin = 174;\n    PARAMETER_KEY_LteRsrqMin = 175;\n    PARAMETER_KEY_BorSettleDelaySec = 176;\n    PARAMETER_KEY_BorRtcRequestGnssEnable = 177;\n    PARAMETER_KEY_BorRtcRequestModemEnable = 178;\n    PARAMETER_KEY_ApnFallbackRunCnt = 179;\n    PARAMETER_KEY_BuzzerPwmFrequency = 180;\n    PARAMETER_KEY_InputOutputMappingConfigs = 181;\n    PARAMETER_KEY_BluetoothNebulaConfiguration = 182;\n    PARAMETER_KEY_ImuWakeupThreshold = 183;\n    PARAMETER_KEY_ImuWakeupDuration = 184;\n    PARAMETER_KEY_ImuWakeupCountThreshold = 185;\n    PARAMETER_KEY_ImuDisplacementTimeWindow = 186;\n    PARAMETER_KEY_AssetRecoveryExpiredDateTime = 187;\n    PARAMETER_KEY_AssetRecoveryOnlyInTrip = 188;\n    PARAMETER_KEY_AssetRecoverySleepInterval = 189;\n}\n\nmessage Parameter\n{\n    ParameterKey key = 1;\n    oneof value {\n        bool value_bool = 2;\n        int32 value_int = 3;\n        uint32 value_uint = 4;\n        bytes value_bytes = 5;\n    }\n}\n\nmessage DeviceParameters\n{\n    repeated Parameter parameters = 1;\n}\n",
                "{\"master_switches\":{\"flags\":\"69Q=\"},\"device_parameters\":{\"parameters\":[{\"key\":\"PARAMETER_KEY_AssetRecoveryOnlyInTrip\",\"value_bool\":false},{\"key\":\"PARAMETER_KEY_AssetRecoverySleepInterval\",\"value_bytes\":\"GG0=\"}]}}"
            },
            {
                "Mappings",
                "syntax = \"proto3\";\n\noption csharp_namespace = \"Contoso.Protobuf\";\noption go_package = \"git.contoso.com/dev/GatewayGoProto-golang/configuration/iox\";\n\nmessage UIntPoints {\n    repeated uint32 input_values = 1;\n    repeated uint32 output_values = 2;\n}\n\nenum DigitalAux {\n    DIGITAL_AUX_1 = 0;\n    DIGITAL_AUX_2 = 1;\n    DIGITAL_AUX_3 = 2;\n    DIGITAL_AUX_4 = 3;\n    DIGITAL_AUX_5 = 4;\n    DIGITAL_AUX_6 = 5;\n    DIGITAL_AUX_7 = 6;\n    DIGITAL_AUX_8 = 7;\n}\n\nmessage Input {\n    oneof input {\n        DigitalAux aux = 1;\n        uint32 status_id = 2;\n    }\n}\n\nmessage Output {\n    oneof output {\n        uint32 status_id = 1;\n    }\n}\n\nmessage Transform {\n    oneof transform {\n        UIntPoints uint_points = 1;\n    }\n}\n\nmessage Mapping {\n    Input input = 1;\n    Output output = 2;\n    Transform transform = 3;\n}\n\nmessage Mappings {\n    repeated Mapping mapping = 1;\n}",
                "{\"mapping\":[{\"input\":{\"status_id\":42},\"output\":{\"status_id\":17},\"transform\":{\"uint_points\":{\"input_values\":[100,200],\"output_values\":[150,250]}}},{\"input\":{\"aux\":\"DIGITAL_AUX_3\"},\"output\":{\"status_id\":33},\"transform\":{\"uint_points\":{\"input_values\":[1,2,3],\"output_values\":[4,5,6]}}}]}"
            },
            {
                "Mappings_old",
                "syntax = \"proto3\";\n\noption csharp_namespace = \"Contoso.Protobuf\";\noption go_package = \"git.contoso.com/dev/GatewayGoProto-golang/configuration/iox\";\n\nmessage IntPoints {\n    repeated int32 input_values = 1;\n    repeated int32 output_values = 2;\n}\n\nmessage FloatPoints {\n    repeated float input_values = 1;\n    repeated float output_values = 2;\n}\n\nenum DigitalAux {\n    DIGITAL_AUX_1 = 0;\n    DIGITAL_AUX_2 = 1;\n    DIGITAL_AUX_3 = 2;\n    DIGITAL_AUX_4 = 3;\n    DIGITAL_AUX_5 = 4;\n    DIGITAL_AUX_6 = 5;\n    DIGITAL_AUX_7 = 6;\n    DIGITAL_AUX_8 = 7;\n}\n\nmessage Input {\n    oneof input {\n        DigitalAux aux = 1;\n        uint32 status_id = 2;\n    }\n}\n\nmessage Output {\n    oneof output {\n        uint32 status_id = 1;\n    }\n}\n\nmessage Transform {\n    oneof transform {\n        IntPoints int_points = 1;\n        FloatPoints float_points = 2;\n    }\n}\n\nmessage Mapping {\n    Input input = 1;\n    Output output = 2;\n    Transform transform = 3;\n}\n\nmessage Mappings_old {\n    repeated Mapping mapping = 1;\n}",
                "{\"mapping\":[{\"input\":{\"status_id\":79},\"output\":{\"status_id\":61},\"transform\":{\"float_points\":{\"input_values\":[217,88],\"output_values\":[225,107]}}},{\"input\":{\"aux\":\"DIGITAL_AUX_1\"},\"output\":{\"status_id\":111},\"transform\":{\"float_points\":{\"input_values\":[121,65],\"output_values\":[108,134]}}}]}"
            },
            {
                "test.DeviceConfigurationExtended",
                "syntax = \"proto3\";\n\npackage test;\n\n// Top-level config message\nmessage DeviceConfigurationExtended {\n  int32 deviceId = 1;\n  string deviceName = 2;\n  DeviceStatus status = 3;\n  NetworkSettings network = 4;\n  repeated SensorConfig sensors = 5;\n  oneof optional_settings {\n    PowerSettings power = 6;\n    GpsSettings gps = 7;\n  }\n}\n\n// Simple enum\nenum DeviceStatus {\n  UNKNOWN = 0;\n  ONLINE = 1;\n  OFFLINE = 2;\n  ERROR = 3;\n}\n\n// Nested message: Network settings\nmessage NetworkSettings {\n  string ipAddress = 1;\n  string subnetMask = 2;\n  string gateway = 3;\n  repeated string dnsServers = 4;\n}\n\n// Repeated nested message: Sensor configuration\nmessage SensorConfig {\n  string type = 1;\n  int32 pin = 2;\n  Threshold threshold = 3;\n\n  message Threshold {\n    float min = 1;\n    float max = 2;\n  }\n}\n\n// Oneof message: Power settings\nmessage PowerSettings {\n  bool batteryOperated = 1;\n  int32 voltage = 2;\n}\n\n// Oneof message: GPS settings\nmessage GpsSettings {\n  bool enabled = 1;\n  int32 updateRateHz = 2;\n}",
                "{\n  \"deviceId\": 101,\n  \"deviceName\": \"EdgeNode-45\",\n  \"status\": \"ONLINE\",\n  \"network\": {\n    \"ipAddress\": \"192.168.1.45\",\n    \"subnetMask\": \"255.255.255.0\",\n    \"gateway\": \"192.168.1.1\",\n    \"dnsServers\": [\"8.8.8.8\", \"8.8.4.4\"]\n  },\n  \"sensors\": [\n    {\n      \"type\": \"temperature\",\n      \"pin\": 5,\n      \"threshold\": {\n        \"min\": -20.0,\n        \"max\": 50.0\n      }\n    },\n    {\n      \"type\": \"humidity\",\n      \"pin\": 6,\n      \"threshold\": {\n        \"min\": 10.0,\n        \"max\": 90.0\n      }\n    }\n  ],\n  \"power\": {\n    \"batteryOperated\": true,\n    \"voltage\": 12\n  }\n}"
            },
            {
                "test.TelemetryData",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage TelemetryData {\n  int64 timestamp = 1;\n  double latitude = 2;\n  double longitude = 3;\n  float speed = 4;\n  float heading = 5;\n}\n",
                "{\n  \"timestamp\": 1653000000000,\n  \"latitude\": 45.4215,\n  \"longitude\": -75.6972,\n  \"speed\": 55.5,\n  \"heading\": 180.0\n}"
            },
            {
                "test.SensorReading",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage SensorReading {\n  string sensor_id = 1;\n  float value = 2;\n  string unit = 3;\n}\n",
                "{\n  \"sensor_id\": \"temp_sensor_1\",\n  \"value\": 23.5,\n  \"unit\": \"C\"\n}"
            },
            {
                "AlarmStatus",
                "syntax = \"proto3\";\n\nenum AlarmLevel {\n  LOW = 0;\n  MEDIUM = 1;\n  HIGH = 2;\n}\n\nmessage AlarmStatus {\n  string alarm_id = 1;\n  AlarmLevel level = 2;\n  bool acknowledged = 3;\n}\n",
                "{\n  \"alarm_id\": \"ALM001\",\n  \"level\": \"HIGH\",\n  \"acknowledged\": false\n}"
            },
            {
                "test.DeviceSettings",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage DeviceSettings {\n  map<string, string> settings = 1;\n}\n",
                "{\n  \"settings\": {\n    \"timezone\": \"UTC\",\n    \"mode\": \"auto\"\n  }\n}"
            },
            {
                "test.FirmwareUpdate",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage FirmwareUpdate {\n  string version = 1;\n  bytes payload = 2;\n}\n",
                "{\n  \"version\": \"2.0.1\",\n  \"payload\": \"SGVsbG8gV29ybGQ=\"\n}"
            },
            {
                "test.NetworkInfo",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage NetworkInfo {\n  string ip_address = 1;\n  string mac_address = 2;\n  repeated string dns_servers = 3;\n}\n",
                "{\n  \"ip_address\": \"192.168.1.100\",\n  \"mac_address\": \"00:1A:2B:3C:4D:5E\",\n  \"dns_servers\": [\"8.8.8.8\", \"8.8.4.4\"]\n}"
            },
            {
                "test.TripSummary",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage TripSummary {\n  int32 trip_id = 1;\n  int64 start_time = 2;\n  int64 end_time = 3;\n  double distance_km = 4;\n}\n",
                "{\n  \"trip_id\": 5678,\n  \"start_time\": 1653100000000,\n  \"end_time\": 1653103600000,\n  \"distance_km\": 25.7\n}"
            },
            {
                "test.LocationEvent",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage LocationEvent {\n  int64 timestamp = 1;\n  double latitude = 2;\n  double longitude = 3;\n  oneof source {\n    string gps_source = 4;\n    string wifi_source = 5;\n  }\n}\n",
                "{\n  \"timestamp\": 1653200000000,\n  \"latitude\": 40.7128,\n  \"longitude\": -74.0060,\n  \"gps_source\": \"GPS\"\n}"
            },
            {
                "test.BatteryStatus",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage BatteryStatus {\n  float voltage = 1;\n  float temperature = 2;\n  bool charging = 3;\n}\n",
                "{\n  \"voltage\": 12.6,\n  \"temperature\": 35.2,\n  \"charging\": true\n}"
            },
            {
                "test.DeviceInfo",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage DeviceInfo {\n  int32 device_id = 1;\n  string serial_number = 2;\n  string firmware_version = 3;\n  bool active = 4;\n  repeated string tags = 5;\n}\n",
                "{\n  \"device_id\": 123,\n  \"serial_number\": \"ABC123\",\n  \"firmware_version\": \"1.2.3\",\n  \"active\": true,\n  \"tags\": [\"fleet\", \"sensor\"]\n}"
            },
            {
                "test.ComplexMessage",
                "syntax = \"proto3\";\n\npackage test;\n\nimport \"google/protobuf/timestamp.proto\";\nimport \"google/protobuf/duration.proto\";\n\nmessage ComplexMessage {\n  // Scalars\n  double double_field = 1;\n  float float_field = 2;\n  int32 int32_field = 3;\n  int64 int64_field = 4;\n  uint32 uint32_field = 5;\n  uint64 uint64_field = 6;\n  sint32 sint32_field = 7;\n  sint64 sint64_field = 8;\n  fixed32 fixed32_field = 9;\n  fixed64 fixed64_field = 10;\n  sfixed32 sfixed32_field = 11;\n  sfixed64 sfixed64_field = 12;\n  bool bool_field = 13;\n  string string_field = 14;\n  bytes bytes_field = 15;\n\n  // Enum\n  enum Status {\n    UNKNOWN = 0;\n    STARTED = 1;\n    IN_PROGRESS = 2;\n    COMPLETED = 3;\n  }\n\n  Status enum_field = 16;\n\n  // Repeated fields (packed and non-packed)\n  repeated int32 repeated_int32_field = 17 [packed = true];\n  repeated string repeated_string_field = 18;\n\n  // Map\n  map<string, int32> string_to_int_map = 19;\n\n  // Nested message\n  message NestedMessage {\n    string nested_field = 1;\n  }\n\n  NestedMessage nested_message = 20;\n\n  // Repeated nested message\n  repeated NestedMessage repeated_nested = 21;\n\n  // Oneof\n  oneof my_oneof {\n    string oneof_string = 22;\n    int32 oneof_int32 = 23;\n    bool oneof_bool = 24;\n  }\n\n  // Well-known types\n  google.protobuf.Timestamp timestamp_field = 25;\n  google.protobuf.Duration duration_field = 26;\n\n  // Optional field (proto3 optional, since proto3 supports optional now)\n  optional string optional_string_field = 28;\n}\n",
                "{\n  \"double_field\": 123.456,\n  \"float_field\": 78.9,\n  \"int32_field\": 12345,\n  \"int64_field\": 9876543210,\n  \"uint32_field\": 4294967295,\n  \"uint64_field\": \"18446744073709551615\",\n  \"sint32_field\": -123,\n  \"sint64_field\": -9876543210,\n  \"fixed32_field\": 1000,\n  \"fixed64_field\": 1000000,\n  \"sfixed32_field\": -1000,\n  \"sfixed64_field\": -1000000,\n  \"bool_field\": true,\n  \"string_field\": \"hello world\",\n  \"bytes_field\": \"SGVsbG8gV29ybGQ=\",\n  \"enum_field\": \"COMPLETED\",\n  \"repeated_int32_field\": [1, 2, 3, 4, 5],\n  \"repeated_string_field\": [\"a\", \"b\", \"c\"],\n  \"string_to_int_map\": {\n    \"one\": 1,\n    \"two\": 2\n  },\n  \"nested_message\": {\n    \"nested_field\": \"nested value\"\n  },\n  \"repeated_nested\": [\n    { \"nested_field\": \"nested 1\" },\n    { \"nested_field\": \"nested 2\" }\n  ],\n  \"oneof_string\": \"chosen oneof value\",\n  \"timestamp_field\": \"2022-04-15T17:20:00.123456789Z\",\n  \"duration_field\": \"3600s\",\n  \"optional_string_field\": \"optional value\"\n}"
            },
            {
                "test.TreeNode",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage TreeNode {\n  string name = 1;\n  repeated TreeNode children = 2;\n}\n",
                "{\n  \"name\": \"root\",\n  \"children\": [\n    { \"name\": \"child1\", \"children\": [] },\n    { \"name\": \"child2\", \"children\": [ { \"name\": \"grandchild1\" } ] }\n  ]\n}"
            },
            {
                "test.Level1",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage Level1 {\n  Level2 level2 = 1;\n\n  message Level2 {\n    Level3 level3 = 1;\n\n    message Level3 {\n      string value = 1;\n    }\n  }\n}\n",
                "{\n  \"level2\": { \"level3\": { \"value\": \"deep!\" } }\n}"
            },
            {
                "test.OneOfExample",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage OneOfExample {\n  oneof test_oneof {\n    string text = 1;\n    int32 number = 2;\n  }\n}\n",
                "{\n  \"text\": \"hello\"\n}"
            },
            {
                "test.OneOfExample",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage OneOfExample {\n  oneof test_oneof {\n    string text = 1;\n    int32 number = 2;\n  }\n}\n",
                "{\n  \"number\": 42\n}"
            },
            {
                "test.EmptyMessage",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage EmptyMessage {}\n",
                "{}"
            },
            {
                "test.PackedRepeated",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage PackedRepeated {\n  repeated int32 int32s = 1 [packed = true];\n  repeated bool bools = 2 [packed = true];\n  repeated double doubles = 3 [packed = true];\n}\n",
                "{\n  \"int32s\": [1, 2, 3],\n  \"bools\": [true, false, true],\n  \"doubles\": [1.1, 2.2, 3.3]\n}"
            },
            {
                "test.MapWithMessage",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage MapWithMessage {\n  map<string, SubMessage> items = 1;\n\n  message SubMessage {\n    int32 id = 1;\n    string desc = 2;\n  }\n}\n",
                "{\n  \"items\": {\n    \"key1\": { \"id\": 1, \"desc\": \"first\" },\n    \"key2\": { \"id\": 2, \"desc\": \"second\" }\n  }\n}"
            },
            {
                "test.WithNestedEnum",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage WithNestedEnum {\n  enum Status {\n    UNKNOWN = 0;\n    ACTIVE = 1;\n    INACTIVE = 2;\n  }\n\n  Status status = 1;\n}\n",
                "{\n  \"status\": \"ACTIVE\"\n}"
            },
            {
                "test.SignedTypes",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage SignedTypes {\n  sint32 sint32_field = 1;\n  sint64 sint64_field = 2;\n  sfixed32 sfixed32_field = 3;\n  sfixed64 sfixed64_field = 4;\n}\n",
                "{\n  \"sint32_field\": -12345,\n  \"sint64_field\": -9876543210,\n  \"sfixed32_field\": -12345,\n  \"sfixed64_field\": -9876543210\n}"
            },
            {
                "test.OptionalString",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage OptionalString {\n  optional string name = 1;\n}\n",
                "{\n  \"name\": \"present\"\n}"
            },
            {
                "test.OptionalString",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage OptionalString {\n  optional string name = 1;\n}\n",
                "{}"
            },
            {
                "test.LargeRepeated",
                "syntax = \"proto3\";\n\npackage test;\n\nmessage LargeRepeated {\n  repeated int32 numbers = 1;\n}\n",
                "{\n  \"numbers\": [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]\n}"
            }
        };

    static void AssertParsedMessagesAreEqual(string messageName, string sampleProtoJson, byte[] bytesToVerify)
    {
        switch (messageName)
        {
            case "contoso.protobuf.configuration.DeviceConfiguration":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceConfiguration>(sampleProtoJson), DeviceConfiguration.Parser.ParseFrom(bytesToVerify));
                break;

            case "Mappings":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<Mappings>(sampleProtoJson), Mappings.Parser.ParseFrom(bytesToVerify));
                break;

            case "Mappings_old":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<Mappings_old>(sampleProtoJson), Mappings_old.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.DeviceConfigurationExtended":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceConfigurationExtended>(sampleProtoJson), DeviceConfigurationExtended.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.DeviceInfo":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceInfo>(sampleProtoJson), DeviceInfo.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.TelemetryData":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<TelemetryData>(sampleProtoJson), TelemetryData.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.SensorReading":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<SensorReading>(sampleProtoJson), SensorReading.Parser.ParseFrom(bytesToVerify));
                break;

            case "AlarmStatus":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<AlarmStatus>(sampleProtoJson), AlarmStatus.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.DeviceSettings":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceSettings>(sampleProtoJson), DeviceSettings.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.FirmwareUpdate":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<FirmwareUpdate>(sampleProtoJson), FirmwareUpdate.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.NetworkInfo":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<NetworkInfo>(sampleProtoJson), NetworkInfo.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.TripSummary":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<TripSummary>(sampleProtoJson), TripSummary.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.LocationEvent":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<LocationEvent>(sampleProtoJson), LocationEvent.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.BatteryStatus":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<BatteryStatus>(sampleProtoJson), BatteryStatus.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.ComplexMessage":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<ComplexMessage>(sampleProtoJson), ComplexMessage.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.TreeNode":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<TreeNode>(sampleProtoJson), TreeNode.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.Level1":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<Level1>(sampleProtoJson), Level1.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.OneOfExample":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<OneOfExample>(sampleProtoJson), OneOfExample.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.EmptyMessage":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<EmptyMessage>(sampleProtoJson), EmptyMessage.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.PackedRepeated":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<PackedRepeated>(sampleProtoJson), PackedRepeated.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.MapWithMessage":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<MapWithMessage>(sampleProtoJson), MapWithMessage.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.WithNestedEnum":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<WithNestedEnum>(sampleProtoJson), WithNestedEnum.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.SignedTypes":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<SignedTypes>(sampleProtoJson), SignedTypes.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.OptionalString":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<OptionalString>(sampleProtoJson), OptionalString.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.LargeRepeated":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<LargeRepeated>(sampleProtoJson), LargeRepeated.Parser.ParseFrom(bytesToVerify));
                break;

            default:
                throw new NotSupportedException($"No parser mapped for '{messageName}'.");
        }
    }
}
