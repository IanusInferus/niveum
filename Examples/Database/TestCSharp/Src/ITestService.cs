using System;
using System.Collections.Generic;

namespace Database
{
    public interface ITestService
    {
        void SaveData(int SessionIndex, int Value);
        int LoadData(int SessionIndex);
        void SaveLockData(int Value);
        void AddLockData(int Value);
        int DeleteLockData();
        int LoadLockData();
    }
}
