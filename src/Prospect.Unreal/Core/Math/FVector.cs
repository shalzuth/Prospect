namespace Prospect.Unreal.Core.Math;

public class FVector
{
    public Single X;
    public Single Y;
    public Single Z;
    public static FVector ZeroVector = new FVector();
    public static FVector OneVector = new FVector(1,1,1);
    public FVector() { X = Y = Z = 0; }
    public FVector(Single x, Single y, Single z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    public override string ToString()
    {
        return $"{{X:{X},Y:{Y},Z:{Z}}}";
    }
    public static Single Distance(FVector from, FVector to)
    {
        var x = from.X - to.X;
        var y = from.Y - to.Y;
        var z = from.Z - to.Z;
        return (Single)System.Math.Sqrt((x * x) + (y * y) + (z * z));
    }
    public static FVector TransformCoordinate(FVector coordinate, float[,] transform)
    {
        var X = (coordinate.X * transform[0, 0]) + (coordinate.Y * transform[1, 0]) + (coordinate.Z * transform[2, 0]) + transform[3, 0];
        var Y = (coordinate.X * transform[0, 1]) + (coordinate.Y * transform[1, 1]) + (coordinate.Z * transform[2, 1]) + transform[3, 1];
        var Z = (coordinate.X * transform[0, 2]) + (coordinate.Y * transform[1, 2]) + (coordinate.Z * transform[2, 2]) + transform[3, 2];
        var W = 1f / ((coordinate.X * transform[0, 3]) + (coordinate.Y * transform[1, 3]) + (coordinate.Z * transform[2, 3]) + transform[3, 3]);
        return new FVector(X * W, Y * W, Z * W);
    }
    public static FVector operator +(FVector vecA, FVector vecB) => new FVector(vecA.X + vecB.X, vecA.Y + vecB.Y, vecA.Z + vecB.Z);
    public static FVector operator -(FVector vecA, FVector vecB) => new FVector(vecA.X - vecB.X, vecA.Y - vecB.Y, vecA.Z - vecB.Z);
    public static Single operator *(FVector vecA, FVector vecB) => vecA.X * vecB.X + vecA.Y * vecB.Y + vecA.Z * vecB.Z;
    public static FVector operator *(FVector vecA, Single n) => new FVector(vecA.X * n, vecA.Y * n, vecA.Z * n);
    public static FVector operator /(FVector vecA, Single n) => new FVector(vecA.X / n, vecA.Y / n, vecA.Z / n);
    public static FVector operator /(FVector vecA, FVector vecB) => new FVector(vecA.X / vecB.X, vecA.Y / vecB.Y, vecA.Z / vecB.Z);
}
public class FVector10 : FVector { }
public class FVector_NetQuantize10 : FVector { }
public class FVector100 : FVector { }
public class FVectorQ : FVector { }
public class FVector2D : FVector { }
public class StdVector2D : FVector { }
public class FFixedVector : FVector { }