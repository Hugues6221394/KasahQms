namespace KasahQMS.Domain.Enums;

/// <summary>
/// CAPA lifecycle status enumeration.
/// CAPA must follow this lifecycle order and cannot skip states:
/// Draft → UnderInvestigation → ActionsDefined → ActionsImplemented → EffectivenessVerified → Closed
/// </summary>
public enum CapaStatus
{
    /// <summary>Initial state when CAPA is created but not yet started</summary>
    Draft = 0,
    
    /// <summary>Root cause investigation is in progress</summary>
    UnderInvestigation = 1,
    
    /// <summary>Corrective/preventive actions have been defined</summary>
    ActionsDefined = 2,
    
    /// <summary>Actions have been implemented</summary>
    ActionsImplemented = 3,
    
    /// <summary>Effectiveness has been verified (cannot delete after this state)</summary>
    EffectivenessVerified = 4,
    
    /// <summary>CAPA is closed (final state)</summary>
    Closed = 5
}
