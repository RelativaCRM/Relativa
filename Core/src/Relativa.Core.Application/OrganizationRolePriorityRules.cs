using Relativa.Core.Application.Exceptions;

namespace Relativa.Core.Application;

/// <summary>Organization role <see cref="OrganizationRolePriorityTiers">priority</see> comparisons for removal / archive.</summary>
public static class OrganizationRolePriorityRules
{
    /// <summary>
    /// Stronger role = lower <paramref name="priority" /> value.
    /// Caller must strictly outrank target: <c>callerPriority &lt; targetPriority</c>.
    /// </summary>
    public static void EnsureCallerOutranksTarget(int callerPriority, int targetPriority)
    {
        if (callerPriority >= targetPriority)
        {
            throw new ForbiddenAccessException(
                "You cannot perform this action on a member whose organization role has equal or higher authority than yours.");
        }
    }
}
