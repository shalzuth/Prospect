using System;
namespace Prospect.Unreal.Core.Math;

public class FRotator : FVector
{
    public Single Pitch { get { return X; } set { X = value; } }
    public Single Yaw { get { return Y; } set { Y = value; } }
    public Single Roll { get { return Z; } set { Z = value; } }
    public static FRotator ZeroRotator = new FRotator { X = 0, Y = 0, Z = 0 };
    public void GetAxes(out FVector x, out FVector y, out FVector z)
    {
        var m = ToMatrix();

        x = new FVector(m[0, 0], m[0, 1], m[0, 2]);
        y = new FVector(m[1, 0], m[1, 1], m[1, 2]);
        z = new FVector(m[2, 0], m[2, 1], m[2, 2]);
    }
    public FVector ToVector()
    {
        float radPitch = (float)(Pitch * System.Math.PI / 180f);
        float radYaw = (float)(Yaw * System.Math.PI / 180f);
        float SP = (float)System.Math.Sin(radPitch);
        float CP = (float)System.Math.Cos(radPitch);
        float SY = (float)System.Math.Sin(radYaw);
        float CY = (float)System.Math.Cos(radYaw);
        return new FVector(CP * CY, CP * SY, SP);
    }
    public Single[,] ToMatrix(FVector? origin = default(FVector))
    {
        if (origin == null)
            origin = FVector.ZeroVector;
        var radPitch = (Single)(Pitch * System.Math.PI / 180f);
        var radYaw = (Single)(Yaw * System.Math.PI / 180f);
        var radRoll = (Single)(Roll * System.Math.PI / 180f);

        var SP = (Single)System.Math.Sin(radPitch);
        var CP = (Single)System.Math.Cos(radPitch);
        var SY = (Single)System.Math.Sin(radYaw);
        var CY = (Single)System.Math.Cos(radYaw);
        var SR = (Single)System.Math.Sin(radRoll);
        var CR = (Single)System.Math.Cos(radRoll);

        var m = new Single[4, 4];
        m[0, 0] = CP * CY;
        m[0, 1] = CP * SY;
        m[0, 2] = SP;
        m[0, 3] = 0f;

        m[1, 0] = SR * SP * CY - CR * SY;
        m[1, 1] = SR * SP * SY + CR * CY;
        m[1, 2] = -SR * CP;
        m[1, 3] = 0f;

        m[2, 0] = -(CR * SP * CY + SR * SY);
        m[2, 1] = CY * SR - CR * SP * SY;
        m[2, 2] = CR * CP;
        m[2, 3] = 0f;

        m[3, 0] = origin.X;
        m[3, 1] = origin.Y;
        m[3, 2] = origin.Z;
        m[3, 3] = 1f;
        return m;
    }
}
