// FS25 Real FFB - interface comum das saidas de forca (WGI ou DirectInput).
using System;

namespace FS25RealFFB
{
    internal interface IWheelOutput : IDisposable
    {
        bool IsOpen { get; }
        string DeviceName { get; }
        void SetSpring(int coef, int sat);
        void SetDamper(int coef, int sat);
        void SetFriction(int coef, int sat);
        void SetConstant(int mag);
        void SetSine(int mag, int periodUs);
        int ReadPosition();
        void EmergencyStop();
        void Commit();
    }
}
