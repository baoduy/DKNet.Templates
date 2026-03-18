using Minimal.Infra.Contexts;

namespace Minimal.Infra.Services;

internal sealed class MembershipService(CoreDbContext dbContext)
    : SequenceService(dbContext, Sequences.Membership), IMembershipService;