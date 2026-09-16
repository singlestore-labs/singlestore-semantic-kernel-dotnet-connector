using SingleStore.SemanticKernel.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit.Sdk;

namespace SingleStore.SemanticKernel.ConformanceTests;

public class SingleStoreFilterTests(SingleStoreFilterTests.Fixture fixture)
    : FilterTests<int>(fixture), IClassFixture<SingleStoreFilterTests.Fixture>
{
    public override async Task Not_over_Or()
    {
        // Test sends: WHERE (NOT (("Int" = 8) OR ("String" = 'foo')))
        // There's a NULL string in the database, and relational null semantics in conjunction with negation makes the default implementation fail.
        await Assert.ThrowsAsync<FailException>(() => base.Not_over_Or());

        // Compensate by adding a null check:
        await TestFilterAsync(
            r => r.String != null && !(r.Int == 8 || r.String == "foo"),
            r => r["String"] != null && !((int)r["Int"]! == 8 || r["String"] == "foo"));
    }

    public override async Task NotEqual_with_string()
    {
        // As above, null semantics + negation
        await Assert.ThrowsAsync<FailException>(() => base.NotEqual_with_string());

        await TestFilterAsync(
            r => r.String != null && r.String != "foo",
            r => r["String"] != null && r["String"] != "foo");
    }

    public new class Fixture : FilterTests<int>.Fixture
    {
        public override TestStore TestStore => SingleStoreTestStore.Instance;
    }
}
