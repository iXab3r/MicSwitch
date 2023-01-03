namespace MicSwitch.Services
{
    internal interface IMMDeviceControllerEx : IMMDeviceController
    {
        IMMDeviceController ActiveController { get; }
    }
}