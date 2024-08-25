// @@@SNIPSTART money-transfer-project-template-dotnet-withdraw-activity

using System.Runtime.CompilerServices;
using Temporalio.Graph;

namespace Temporalio.Graph;

public class DecisionsBase
{
    public string Id([CallerMemberName] string name = "")
    {
        var indexOfProperty = this.GetType().GetProperties()
            .Where(p => p.GetAttributes<DecisionAttribute>().Any())
            .TakeWhile(p => p.Name != name)
            .Count();
        return $"decision{indexOfProperty}";
    }
}

public class DecisionAttribute : Attribute
{
    // can add a member to store Decision description
}

