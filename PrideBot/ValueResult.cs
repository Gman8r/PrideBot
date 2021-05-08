using System;

namespace PrideBot
{
    public struct ValueResult<T>
    {
        public T Value { get; private set; }
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }


        public static ValueResult<T> Success(T value) => new ValueResult<T>
        {
            Value = value,
            IsSuccess = true
        };
        public static ValueResult<T> Error(string errorMessage) => new ValueResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
        public static ValueResult<T> Error(Exception ex) => new ValueResult<T>
        {
            IsSuccess = false,
            ErrorMessage = ex.Message
        };

    }
}