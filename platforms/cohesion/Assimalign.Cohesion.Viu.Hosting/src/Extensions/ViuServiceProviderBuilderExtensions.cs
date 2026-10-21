using System;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Registers Viu component composition in the application's Cohesion service container.</summary>
public static class ViuServiceProviderBuilderExtensions
{
    /// <param name="services">The service-provider builder that owns the application composition.</param>
    extension(IServiceProviderBuilder services)
    {
        /// <summary>Contributes the application's compile-time aggregated component catalogs.</summary>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>Repeated generated contributions are idempotent for each component factory.</remarks>
        public IServiceProviderBuilder AddComponents() =>
            services.AddComponents(static components => components.RegisterGeneratedComponents());

        /// <summary>
        /// Contributes components through an explicit registration callback, such as a generated
        /// <c>GeneratedViuComponents.Register</c> catalog.
        /// </summary>
        /// <param name="register">The callback that registers components into the application factory.</param>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>
        /// Contributions run once, in registration order, when <see cref="ViuApplicationBuilder.Build"/>
        /// composes the application's <see cref="IComponentFactory"/>, so registration order relative
        /// to <see cref="ViuApplicationBuilder.ConfigureApplication"/> does not matter. A factory
        /// assigned through <c>ConfigureApplication</c> takes precedence over contributed components.
        /// Registering a different component under an already registered reference fails the build.
        /// </remarks>
        public IServiceProviderBuilder AddComponents(Action<ComponentFactory> register)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(register);

            ViuComponentComposition.GetOrAdd(services);
            return services.Add(ServiceDescriptor.Singleton(new ViuComponentContribution(register)));
        }

        /// <summary>Contributes one explicit component registration.</summary>
        /// <param name="registration">The reflection-free component registration.</param>
        /// <returns>The same service-provider builder.</returns>
        /// <remarks>Contributing the same registration instance more than once registers it once.</remarks>
        public IServiceProviderBuilder AddComponent(ComponentRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registration);

            return services.AddComponents(components =>
            {
                if (!components.TryResolve(registration.Reference, out ComponentRegistration? existing)
                    || !ReferenceEquals(existing, registration))
                {
                    components.Register(registration);
                }
            });
        }
    }
}
