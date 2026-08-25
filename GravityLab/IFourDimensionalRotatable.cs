namespace GravityLab
{
    /// <summary>
    /// Something whose orientation in four dimensions can be driven from outside, whether it
    /// renders as a cross-section or as a projection. Lets one driver feed either.
    /// </summary>
    public interface IFourDimensionalRotatable
    {
        /// <summary>
        /// Whether the geometry is due to be rebuilt this frame. A driver can check this to
        /// avoid computing angles whose result would be thrown away.
        /// </summary>
        bool isRebuildDue { get; }

        /// <summary>Angle in one of the six rotation planes, in degrees.</summary>
        float GetAngle(TesseractSlice.RotationPlane plane);

        /// <summary>Sets the angle in one of the six rotation planes, in degrees.</summary>
        void SetAngle(TesseractSlice.RotationPlane plane, float degrees);

        /// <summary>Sets how fast one rotation plane turns on its own, in degrees per second.</summary>
        void SetSpeed(TesseractSlice.RotationPlane plane, float degreesPerSecond);
    }
}
