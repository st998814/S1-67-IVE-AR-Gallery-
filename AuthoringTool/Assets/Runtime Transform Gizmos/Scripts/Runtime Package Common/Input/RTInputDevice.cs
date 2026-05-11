//#define INPUT_DEVICE_VR_CONTROLLER
using UnityEngine;

namespace RTG
{
    public class RTInputDevice : MonoSingleton<RTInputDevice>
    {
        private IInputDevice _inputDevice;

        public IInputDevice Device { get { EnsureInputDeviceInitialized(); return _inputDevice; } }
        public InputDeviceType DeviceType { get { EnsureInputDeviceInitialized(); return _inputDevice.DeviceType; } }

        public void Update_SystemCall()
        {
            EnsureInputDeviceInitialized();
            _inputDevice.Update();
        }

        /// <summary>
        /// 若未在 Awake 中完成初始化（例如定义了 INPUT_DEVICE_VR_CONTROLLER 但未实现赋值），避免每帧 NRE。
        /// </summary>
        private void EnsureInputDeviceInitialized()
        {
            if (_inputDevice != null)
                return;

            #if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID || UNITY_WP_8_1)
            _inputDevice = new TouchInputDevice(10);
            #elif INPUT_DEVICE_VR_CONTROLLER
            // 替换为 VR 实现前，使用鼠标设备避免空引用。
            _inputDevice = new MouseInputDevice();
            #else
            _inputDevice = new MouseInputDevice();
            #endif
        }

        private void Awake()
        {
            EnsureInputDeviceInitialized();
        }
    }
}
