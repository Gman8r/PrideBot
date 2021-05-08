using System;

namespace PrideBot
{
    public struct Result
    {
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }

        public Result(bool isSuccess, string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static Result Success()
            => new Result(true, null);
        public static Result Error(string reason)
            => new Result(false, reason);
        public static Result Error(Exception ex)
            => Error(ex.Message);

    }
}