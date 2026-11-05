using System;

namespace Capstone.Common;

public class LookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public class IdInput
{
    public Guid Id { get; set; }
}

public class SetActiveInput
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

public class DateRangeInput
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}

public class WarningDto
{
    public string Kind { get; set; }
    public string Message { get; set; }
}
