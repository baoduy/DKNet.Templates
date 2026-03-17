using SlimBus.Infra.Contexts;

namespace SlimBus.Infra.Services;

internal sealed class MembershipService(CoreDbContext dbContext)
    : SequenceService(dbContext, Sequences.Membership), IMembershipService;