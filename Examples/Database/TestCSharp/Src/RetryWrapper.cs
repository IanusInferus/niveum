using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database
{
    public class RetryWrapper : ITestService
    {
        private ITestService Inner;
        private Func<Exception, Boolean> IsRetryable;
        private int MaxRetryCount;

        public RetryWrapper(ITestService Inner, Func<Exception, Boolean> IsRetryable, int MaxRetryCount)
        {
            this.Inner = Inner;
            this.IsRetryable = IsRetryable;
            this.MaxRetryCount = MaxRetryCount;
        }

        public void SaveData(int SessionIndex, int Value)
        {
            var RetryCount = 0;
            while (RetryCount < MaxRetryCount)
            {
                try
                {
                    Inner.SaveData(SessionIndex, Value);
                    return;
                }
                catch (Exception ex)
                {
                    if (IsRetryable(ex))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("MaxRetryCountReached");
        }

        public int LoadData(int SessionIndex)
        {
            var RetryCount = 0;
            while (RetryCount < MaxRetryCount)
            {
                try
                {
                    return Inner.LoadData(SessionIndex);
                }
                catch (Exception ex)
                {
                    if (IsRetryable(ex))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("MaxRetryCountReached");
        }

        public void SaveLockData(int Value)
        {
            var RetryCount = 0;
            while (RetryCount < MaxRetryCount)
            {
                try
                {
                    Inner.SaveLockData(Value);
                    return;
                }
                catch (Exception ex)
                {
                    if (IsRetryable(ex))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("MaxRetryCountReached");
        }

        public void AddLockData(int Value)
        {
            var RetryCount = 0;
            while (RetryCount < MaxRetryCount)
            {
                try
                {
                    Inner.AddLockData(Value);
                    return;
                }
                catch (Exception ex)
                {
                    if (IsRetryable(ex))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("MaxRetryCountReached");
        }

        public int DeleteLockData()
        {
            var RetryCount = 0;
            while (RetryCount < MaxRetryCount)
            {
                try
                {
                    return Inner.DeleteLockData();
                }
                catch (Exception ex)
                {
                    if (IsRetryable(ex))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("MaxRetryCountReached");
        }

        public int LoadLockData()
        {
            var RetryCount = 0;
            while (RetryCount < MaxRetryCount)
            {
                try
                {
                    return Inner.LoadLockData();
                }
                catch (Exception ex)
                {
                    if (IsRetryable(ex))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("MaxRetryCountReached");
        }
    }
}
