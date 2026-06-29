import Combine
import Foundation

public struct AnyScheduler<Time: Strideable, Options>: Scheduler where Time.Stride: SchedulerTimeIntervalConvertible {
    public typealias SchedulerTimeType = Time
    public typealias SchedulerOptions = Options

    private let _now: () -> Time
    private let _minimumTolerance: () -> Time.Stride
    private let _schedule: (Options?, @escaping () -> Void) -> Void
    private let _scheduleAfter: (Time, Time.Stride, Options?, @escaping () -> Void) -> Void
    private let _scheduleInterval: (Time, Time.Stride, Time.Stride, Options?, @escaping () -> Void) -> Cancellable

    public init<S: Scheduler>(_ scheduler: S) where S.SchedulerTimeType == Time, S.SchedulerOptions == Options {
        _now = { scheduler.now }
        _minimumTolerance = { scheduler.minimumTolerance }
        _schedule = scheduler.schedule
        _scheduleAfter = scheduler.schedule
        _scheduleInterval = scheduler.schedule
    }

    public var now: Time {
        _now()
    }

    public var minimumTolerance: Time.Stride {
        _minimumTolerance()
    }

    public func schedule(options: Options?, _ action: @escaping () -> Void) {
        _schedule(options, action)
    }

    public func schedule(after date: Time, tolerance: Time.Stride, options: Options?, _ action: @escaping () -> Void) {
        _scheduleAfter(date, tolerance, options, action)
    }

    public func schedule(
        after date: Time,
        interval: Time.Stride,
        tolerance: Time.Stride,
        options: Options?,
        _ action: @escaping () -> Void
    ) -> Cancellable {
        _scheduleInterval(date, interval, tolerance, options, action)
    }
}

public typealias AnySchedulerOf<S: Scheduler> = AnyScheduler<S.SchedulerTimeType, S.SchedulerOptions>

public extension Scheduler {
    func eraseToAnyScheduler() -> AnyScheduler<SchedulerTimeType, SchedulerOptions> {
        AnyScheduler(self)
    }
}

public extension AnyScheduler where Time == DispatchQueue.SchedulerTimeType, Options == DispatchQueue.SchedulerOptions {
    static var main: Self {
        DispatchQueue.main.eraseToAnyScheduler()
    }
}
