﻿using Common.Helpers;

namespace Common.Enums
{
    [Flags]
    public enum OSEnum : byte
    {
        Windows = 2,
        Linux = 4
    }

    public static class OSEnumHelper
    {
        public static OSEnum AddFlag(this OSEnum osenum, OSEnum flag)
        {
            return osenum |= flag;
        }

        public static OSEnum RemoveFlag(this OSEnum osenum, OSEnum flag)
        {
            return osenum &= ~flag;
        }

        /// <summary>
        /// Get current OS
        /// </summary>
        public static OSEnum GetCurrentOSEnum()
        {
            if (OperatingSystem.IsWindows())
            {
                return OSEnum.Windows;
            }
            else if (OperatingSystem.IsLinux())
            {
                return OSEnum.Linux;
            }

            return ThrowHelper.PlatformNotSupportedException<OSEnum>("Error while identifying platform");
        }
    }
}
