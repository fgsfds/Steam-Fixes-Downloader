﻿namespace Common.Messages
{
    public sealed class RatingMessage
    {
        public required Guid FixGuid { get; set; }
        public required sbyte Score { get; set; }
    }
}
