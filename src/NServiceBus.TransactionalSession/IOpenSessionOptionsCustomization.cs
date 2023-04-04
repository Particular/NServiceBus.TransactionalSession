namespace NServiceBus.TransactionalSession
{
    using System.Collections.Generic;

    /// <summary>
    /// An extension point that allows for customization of settings when opening the <see cref="ITransactionalSession"/>.
    /// </summary>
    /// TODO: Obsolete with warn?
    public interface IOpenSessionOptionsCustomization
    {
        /// <summary>
        /// Applies custom settings to the specified <paramref name="options"/>
        /// </summary>
        /// <param name="options">The options to which the customizations are applied</param>
        void Apply(OpenSessionOptions options);
    }

    /// <summary>
    /// An extension point that allows for customization of settings when opening the <see cref="ITransactionalSession"/>.
    /// </summary>
    public abstract class OpenSessionOptionCustomization
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="options"></param>
        public virtual void ApplyBeforeOpen(OpenSessionOptions options)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="options"></param>
        public virtual void ApplyAfterOpen(OpenSessionOptions options)
        {
        }
    }

    // TODO: Finish this
    sealed class Customization : OpenSessionOptionCustomization
    {
        readonly IEnumerable<IOpenSessionOptionsCustomization> customizations;

        public Customization(IEnumerable<IOpenSessionOptionsCustomization> customizations)
        {
            this.customizations = customizations;
        }

        public override void ApplyAfterOpen(OpenSessionOptions options)
        {
            foreach (var customization in customizations)
            {
                customization.Apply(options);
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    public abstract class OpenSessionOptionCustomization<TOptions> : OpenSessionOptionCustomization
        where TOptions : OpenSessionOptions
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="options"></param>
        public sealed override void ApplyBeforeOpen(OpenSessionOptions options)
        {
            if (options is TOptions concreteOptions)
            {
                ApplyBeforeOpen(concreteOptions);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="options"></param>
        public sealed override void ApplyAfterOpen(OpenSessionOptions options)
        {
            if (options is TOptions concreteOptions)
            {
                ApplyAfterOpen(concreteOptions);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="options"></param>
        public virtual void ApplyBeforeOpen(TOptions options)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="options"></param>
        public virtual void ApplyAfterOpen(TOptions options)
        {
        }
    }
}