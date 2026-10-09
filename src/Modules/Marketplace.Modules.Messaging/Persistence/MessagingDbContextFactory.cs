using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Messaging.Persistence;

/// <summary>Lets dotnet ef build <see cref="MessagingDbContext"/> at design time.</summary>
internal sealed class MessagingDbContextFactory : ModuleDesignTimeFactory<MessagingDbContext>
{
    protected override string Schema => MessagingModule.Schema;
}