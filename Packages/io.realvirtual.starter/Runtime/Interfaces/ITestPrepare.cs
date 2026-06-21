

namespace realvirtual
{
    public interface ITestPrepare
    {
        public void Prepare();
    }

    //! Static registry for test timing. Set by FeatureTestRunner, read by TestModelController.
    public static class TestTimeRegistry
    {
        //! Maximum MinTestTime across all feature tests. 0 means no feature tests.
        public static float MaxRequiredTestTime { get; set; }
    }
}

