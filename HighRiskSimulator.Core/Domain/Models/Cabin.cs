using System;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Entidad de cabina del sistema.
/// 
/// Mantiene su propio estado cinemático, carga actual y temporizadores de recuperación
/// para desacoplar la lógica de simulación del detalle de la UI.
/// </summary>
public sealed class Cabin
{
    public Cabin(
        int id,
        string code,
        int capacity,
        int assignedSegmentId,
        TravelDirection initialDirection,
        double initialSegmentPositionMeters,
        TimeSpan defaultStationDwellTime)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El código de la cabina es obligatorio.", nameof(code));
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad debe ser positiva.");
        }

        if (initialSegmentPositionMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSegmentPositionMeters), "La posición inicial no puede ser negativa.");
        }

        if (defaultStationDwellTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultStationDwellTime), "El tiempo base de permanencia no puede ser negativo.");
        }

        Id = id;
        Code = code;
        Capacity = capacity;
        AssignedSegmentId = assignedSegmentId;
        Direction = initialDirection;
        SegmentPositionMeters = initialSegmentPositionMeters;
        DefaultStationDwellTime = defaultStationDwellTime;
        OperationalState = CabinOperationalState.IdleAtStation;
        AlertLevel = CabinAlertLevel.Normal;
        MechanicalHealth = 1.0;
        ElectricalHealth = 1.0;
        BrakeHealth = 1.0;
        RemainingDwellTime = defaultStationDwellTime;
    }

    public int Id { get; }

    public string Code { get; }

    public int Capacity { get; }

    public int AssignedSegmentId { get; }

    public TravelDirection Direction { get; private set; }

    public CabinOperationalState OperationalState { get; private set; }

    public CabinAlertLevel AlertLevel { get; private set; }

    public double SegmentPositionMeters { get; private set; }

    public double VelocityMetersPerSecond { get; private set; }

    public double AccelerationMetersPerSecondSquared { get; private set; }

    public int PassengerCount { get; private set; }

    public double MechanicalHealth { get; private set; }

    public double ElectricalHealth { get; private set; }

    public double BrakeHealth { get; private set; }

    public TimeSpan DefaultStationDwellTime { get; }

    public TimeSpan RemainingDwellTime { get; private set; }

    public bool IsEmergencyBrakeActive { get; private set; }

    public bool HasMechanicalFailure { get; private set; }

    public bool HasElectricalFailure { get; private set; }

    public bool IsOutOfService { get; private set; }

    public TimeSpan MechanicalRecoveryRemaining { get; private set; }

    public TimeSpan ElectricalRecoveryRemaining { get; private set; }

    public TimeSpan EmergencyBrakeRemaining { get; private set; }

    public TimeSpan OutOfServiceRemaining { get; private set; }

    public bool IsOverloaded => PassengerCount > Capacity;

    public double OccupancyRatio => Capacity <= 0 ? 0.0 : (double)PassengerCount / Capacity;

    /// <summary>
    /// Posición proyectada sobre el ciclo ida/vuelta del tramo. Esta abstracción
    /// es útil para procesos de despacho cíclico y para la lista circular.
    /// </summary>
    public double GetRoundTripCyclePosition(double segmentLengthMeters)
    {
        return Direction == TravelDirection.Ascending
            ? SegmentPositionMeters
            : segmentLengthMeters + (segmentLengthMeters - SegmentPositionMeters);
    }

    public void SetAlertLevel(CabinAlertLevel level)
    {
        AlertLevel = level;
    }

    public void SetPassengers(int passengers)
    {
        PassengerCount = Math.Max(0, passengers);
    }

    public int BoardPassengers(int requestedPassengers)
    {
        var boarded = Math.Max(0, requestedPassengers);
        PassengerCount += boarded;
        return boarded;
    }

    public int UnloadAllPassengers()
    {
        var unloaded = PassengerCount;
        PassengerCount = 0;
        return unloaded;
    }

    public void StartStationStop(TimeSpan? dwellTime = null)
    {
        RemainingDwellTime = dwellTime ?? DefaultStationDwellTime;
        VelocityMetersPerSecond = 0;
        AccelerationMetersPerSecondSquared = 0;
        OperationalState = IsOutOfService
            ? CabinOperationalState.OutOfService
            : CabinOperationalState.IdleAtStation;
    }

    public void ReduceDwell(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "El delta de tiempo no puede ser negativo.");
        }

        RemainingDwellTime = RemainingDwellTime <= TimeSpan.Zero
            ? TimeSpan.Zero
            : RemainingDwellTime - delta;

        if (RemainingDwellTime < TimeSpan.Zero)
        {
            RemainingDwellTime = TimeSpan.Zero;
        }
    }

    public void ReverseDirection()
    {
        Direction = Direction == TravelDirection.Ascending
            ? TravelDirection.Descending
            : TravelDirection.Ascending;
    }

    public void UpdateMotion(double newPosition, double newVelocity, double newAcceleration, CabinOperationalState newState)
    {
        SegmentPositionMeters = Math.Max(0, newPosition);
        VelocityMetersPerSecond = Math.Max(0, newVelocity);
        AccelerationMetersPerSecondSquared = newAcceleration;
        OperationalState = IsOutOfService ? CabinOperationalState.OutOfService : newState;
    }

    public void ApplyMechanicalDamage(double amount, bool forceFailure, TimeSpan failureDuration)
    {
        MechanicalHealth = Math.Clamp(MechanicalHealth - Math.Max(0, amount), 0.0, 1.0);

        if (forceFailure || MechanicalHealth <= 0.45)
        {
            HasMechanicalFailure = true;
            MechanicalRecoveryRemaining = failureDuration;
            OperationalState = CabinOperationalState.Faulted;
        }
    }

    public void ApplyElectricalDamage(double amount, bool forceFailure, TimeSpan failureDuration)
    {
        ElectricalHealth = Math.Clamp(ElectricalHealth - Math.Max(0, amount), 0.0, 1.0);

        if (forceFailure || ElectricalHealth <= 0.45)
        {
            HasElectricalFailure = true;
            ElectricalRecoveryRemaining = failureDuration;
            OperationalState = CabinOperationalState.Faulted;
        }
    }

    public void ApplyBrakeDamage(double amount)
    {
        BrakeHealth = Math.Clamp(BrakeHealth - Math.Max(0, amount), 0.15, 1.0);
    }

    public void ActivateEmergencyBrake(TimeSpan duration)
    {
        IsEmergencyBrakeActive = true;
        EmergencyBrakeRemaining = EmergencyBrakeRemaining > duration
            ? EmergencyBrakeRemaining
            : duration;
    }

    public void ClearEmergencyBrake()
    {
        if (!HasMechanicalFailure && !HasElectricalFailure && !IsOutOfService)
        {
            IsEmergencyBrakeActive = false;
            EmergencyBrakeRemaining = TimeSpan.Zero;
        }
    }

    public void MarkOutOfService(TimeSpan duration)
    {
        IsOutOfService = true;
        OutOfServiceRemaining = duration;
        VelocityMetersPerSecond = 0;
        AccelerationMetersPerSecondSquared = 0;
        OperationalState = CabinOperationalState.OutOfService;
    }

    public void RestoreToService()
    {
        if (!HasMechanicalFailure && !HasElectricalFailure)
        {
            IsOutOfService = false;
            OutOfServiceRemaining = TimeSpan.Zero;
            OperationalState = RemainingDwellTime > TimeSpan.Zero
                ? CabinOperationalState.IdleAtStation
                : CabinOperationalState.Cruising;
        }
    }

    /// <summary>
    /// Avanza todos los temporizadores internos de recuperación y protección.
    /// </summary>
    public void AdvanceFaultTimers(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "El delta no puede ser negativo.");
        }

        if (MechanicalRecoveryRemaining > TimeSpan.Zero)
        {
            MechanicalRecoveryRemaining -= delta;
            if (MechanicalRecoveryRemaining <= TimeSpan.Zero)
            {
                MechanicalRecoveryRemaining = TimeSpan.Zero;
                HasMechanicalFailure = false;
            }
        }

        if (ElectricalRecoveryRemaining > TimeSpan.Zero)
        {
            ElectricalRecoveryRemaining -= delta;
            if (ElectricalRecoveryRemaining <= TimeSpan.Zero)
            {
                ElectricalRecoveryRemaining = TimeSpan.Zero;
                HasElectricalFailure = false;
            }
        }

        if (EmergencyBrakeRemaining > TimeSpan.Zero)
        {
            EmergencyBrakeRemaining -= delta;
            if (EmergencyBrakeRemaining <= TimeSpan.Zero)
            {
                EmergencyBrakeRemaining = TimeSpan.Zero;
                ClearEmergencyBrake();
            }
        }

        if (OutOfServiceRemaining > TimeSpan.Zero)
        {
            OutOfServiceRemaining -= delta;
            if (OutOfServiceRemaining <= TimeSpan.Zero)
            {
                OutOfServiceRemaining = TimeSpan.Zero;
                RestoreToService();
            }
        }

        if (!HasMechanicalFailure && !HasElectricalFailure && !IsOutOfService && !IsEmergencyBrakeActive && RemainingDwellTime > TimeSpan.Zero)
        {
            OperationalState = CabinOperationalState.IdleAtStation;
        }
    }

    public Cabin Clone()
    {
        var clone = new Cabin(Id, Code, Capacity, AssignedSegmentId, Direction, SegmentPositionMeters, DefaultStationDwellTime);
        clone.SetPassengers(PassengerCount);
        clone.MechanicalHealth = MechanicalHealth;
        clone.ElectricalHealth = ElectricalHealth;
        clone.BrakeHealth = BrakeHealth;
        clone.OperationalState = OperationalState;
        clone.AlertLevel = AlertLevel;
        clone.VelocityMetersPerSecond = VelocityMetersPerSecond;
        clone.AccelerationMetersPerSecondSquared = AccelerationMetersPerSecondSquared;
        clone.RemainingDwellTime = RemainingDwellTime;
        clone.IsEmergencyBrakeActive = IsEmergencyBrakeActive;
        clone.HasMechanicalFailure = HasMechanicalFailure;
        clone.HasElectricalFailure = HasElectricalFailure;
        clone.IsOutOfService = IsOutOfService;
        clone.MechanicalRecoveryRemaining = MechanicalRecoveryRemaining;
        clone.ElectricalRecoveryRemaining = ElectricalRecoveryRemaining;
        clone.EmergencyBrakeRemaining = EmergencyBrakeRemaining;
        clone.OutOfServiceRemaining = OutOfServiceRemaining;
        return clone;
    }

    public override string ToString()
    {
        return $"{Code} ({PassengerCount}/{Capacity})";
    }
}
