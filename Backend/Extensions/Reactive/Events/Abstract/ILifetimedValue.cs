﻿namespace Extensions.Abstract
{
    public interface ILifetimedValue<T> : IEventSource<IReadOnlyLifetime, T>
    {
        T Value { get; }
		IReadOnlyLifetime ValueLifetime { get; }
    }
}