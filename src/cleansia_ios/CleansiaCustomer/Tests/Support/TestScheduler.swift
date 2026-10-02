import Combine
import Foundation

final class TestScheduler<Time: Strideable, Options>: Scheduler where Time.Stride: SchedulerTimeIntervalConvertible {
    typealias SchedulerTimeType = Time
    typealias SchedulerOptions = Options

    private(set) var now: SchedulerTimeType
    var minimumTolerance: SchedulerTimeType.Stride = .zero

    private var lastSequence: UInt = 0
    // swiftlint:disable:next large_tuple
    private typealias Entry = (sequence: UInt, date: SchedulerTimeType, action: () -> Void)
    private var scheduled: [Entry] = []

    init(now: SchedulerTimeType) {
        self.now = now
    }

    func schedule(options _: SchedulerOptions?, _ action: @escaping () -> Void) {
        enqueue(date: now, action: action)
    }

    func schedule(
        after date: SchedulerTimeType,
        tolerance _: SchedulerTimeType.Stride,
        options _: SchedulerOptions?,
        _ action: @escaping () -> Void
    ) {
        enqueue(date: date, action: action)
    }

    func schedule(
        after date: SchedulerTimeType,
        interval: SchedulerTimeType.Stride,
        tolerance _: SchedulerTimeType.Stride,
        options _: SchedulerOptions?,
        _ action: @escaping () -> Void
    ) -> Cancellable {
        let sequence = nextSequence()
        func runLoop(_ date: SchedulerTimeType) {
            enqueue(sequence: sequence, date: date) {
                action()
                runLoop(date.advanced(by: interval))
            }
        }
        runLoop(date)
        return AnyCancellable { [weak self] in
            self?.scheduled.removeAll { $0.sequence == sequence }
        }
    }

    func advance(by stride: SchedulerTimeType.Stride) {
        let target = now.advanced(by: stride)
        while let next = scheduled.filter({ $0.date <= target }).min(by: lessThan) {
            now = next.date
            scheduled.removeAll { $0.sequence == next.sequence }
            next.action()
        }
        now = target
    }

    private func enqueue(sequence: UInt? = nil, date: SchedulerTimeType, action: @escaping () -> Void) {
        scheduled.append((sequence ?? nextSequence(), date, action))
    }

    private func nextSequence() -> UInt {
        lastSequence += 1
        return lastSequence
    }

    private func lessThan(_ lhs: Entry, _ rhs: Entry) -> Bool {
        lhs.date == rhs.date ? lhs.sequence < rhs.sequence : lhs.date < rhs.date
    }
}

extension TestScheduler where Time == DispatchQueue.SchedulerTimeType, Options == DispatchQueue.SchedulerOptions {
    static var dispatch: TestScheduler<DispatchQueue.SchedulerTimeType, DispatchQueue.SchedulerOptions> {
        .init(now: DispatchQueue.SchedulerTimeType(DispatchTime(uptimeNanoseconds: 1)))
    }
}
