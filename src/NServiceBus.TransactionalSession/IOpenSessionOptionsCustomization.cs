namespace NServiceBus.TransactionalSession;

/// <summary>
/// An extension point that allows for customization of settings when opening the <see cref="ITransactionalSession"/>.
/// </summary>
public interface IOpenSessionOptionsCustomization
{
    /// <summary>
    /// Applies custom settings to the specified <paramref name="options"/>
    /// </summary>
    /// <param name="options">The options to which the customizations are applied</param>
    void Apply(OpenSessionOptions options);
}