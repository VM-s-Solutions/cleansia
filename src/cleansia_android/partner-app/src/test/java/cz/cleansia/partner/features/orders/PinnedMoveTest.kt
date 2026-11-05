package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem
import androidx.compose.ui.unit.Constraints
import cz.cleansia.partner.api.model.OrderStatus
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The detail's one move per state, and the rule behind every one: it is never a control the server
 * refuses at that moment (set off before start − 60, Take on an overlapping job, Complete without the after photo or
 * with a crew part open, Complete on a card job Stripe has not charged).
 */
class PinnedMoveTest {

    private val prague: ZoneId = ZoneId.of("Europe/Prague")

    private fun at(day: Int, hm: String): Instant = LocalDate.of(2026, 11, day).atTime(LocalTime.parse(hm)).atZone(prague).toInstant()

    private val a1Start = at(12, "14:00")
    private val a2 = OrderListItem(id = "A2", cleaningDateTime = at(13, "15:00").toString(), estimatedTime = 180)

    private fun mine(status: OrderStatus, now: Instant, start: Instant = a1Start, minutes: Int = 120, block: (PinnedMoveArgs) -> PinnedMoveArgs = { it }) =
        block(PinnedMoveArgs(status, true, now, start, minutes)).move()

    /** The arguments, so each case names only what it changes. */
    private data class PinnedMoveArgs(
        val status: OrderStatus?,
        val mine: Boolean,
        val now: Instant,
        val start: Instant?,
        val minutes: Int,
        val preferred: Boolean = false,
        val clash: OrderListItem? = null,
        val ownRowsAnswered: Boolean = true,
        val step: CompletionStep = CompletionStep.Complete,
        val afterPhoto: Boolean = false,
        val cash: Boolean = false,
        val canReport: Boolean = false,
        val charged: Boolean = true,
        val waitingFor: CrewSeat? = null,
        val mateMarked: Boolean = false,
    ) {
        fun move() = pinnedMove(
            status = status, isAssignedToCurrentUser = mine, now = now, start = start, minutes = minutes, preferred = preferred,
            clash = clash, ownRowsAnswered = ownRowsAnswered, completionStep = step, hasAfterPhoto = afterPhoto,
            needsCashCollection = cash, canReportCashNotPaid = canReport, cardCharged = charged, waitingFor = waitingFor,
            mateMarked = mateMarked,
        )
    }

    private fun notMine(status: OrderStatus, now: Instant, start: Instant, minutes: Int, block: (PinnedMoveArgs) -> PinnedMoveArgs = { it }) =
        block(PinnedMoveArgs(status, false, now, start, minutes)).move()

    @Test
    fun `browse, a job overlapping her own is Take disabled with the job it hits`() {
        assertEquals(PinnedMove.TakeOverlaps(a2), notMine(OrderStatus._0, at(12, "09:20"), at(13, "14:00"), 120) { it.copy(clash = a2) })
    }

    @Test
    fun `no board row, Take waits for her own rows, and a failed read offers it with the server as the guard`() {
        assertEquals(PinnedMove.TakeWaiting(preferred = false), notMine(OrderStatus._0, at(12, "09:20"), at(13, "14:00"), 120) { it.copy(ownRowsAnswered = false) })
        assertEquals(PinnedMove.Take(preferred = false), notMine(OrderStatus._0, at(12, "09:20"), at(13, "14:00"), 120))
    }

    @Test
    fun `reserved for her, Confirm with Decline`() {
        assertEquals(PinnedMove.Take(preferred = true), notMine(OrderStatus._0, at(12, "09:20"), at(14, "10:00"), 120) { it.copy(preferred = true) })
    }

    @Test
    fun `reserved for her while her rows load, the Confirm and Decline pair with Confirm waiting`() {
        assertEquals(
            PinnedMove.TakeWaiting(preferred = true),
            notMine(OrderStatus._0, at(12, "09:20"), at(14, "10:00"), 120) { it.copy(preferred = true, ownRowsAnswered = false) },
        )
    }

    @Test
    fun `late joiner, Take on statuses 3 and 4 before the booked end, and nothing from the end on`() {
        val v1Start = at(12, "08:00")
        assertEquals(PinnedMove.Take(false), notMine(OrderStatus._4, at(12, "09:20"), v1Start, 240))
        assertEquals(PinnedMove.Take(false), notMine(OrderStatus._3, at(12, "09:20"), v1Start, 240))
        assertEquals(PinnedMove.None, notMine(OrderStatus._4, at(12, "12:00"), v1Start, 240))
        assertEquals(PinnedMove.None, notMine(OrderStatus._3, at(12, "12:00"), v1Start, 240))
    }

    @Test
    fun `set off is locked until start - 60, then the slide, and past the start it also offers the start`() {
        assertEquals(PinnedMove.SetOffLocked(at(12, "13:00")), mine(OrderStatus._2, at(12, "09:20")))
        assertEquals(PinnedMove.SetOffLocked(at(12, "13:00")), mine(OrderStatus._2, at(12, "12:59")))
        assertEquals(PinnedMove.SetOff(late = false), mine(OrderStatus._2, at(12, "13:00")))
        assertEquals(PinnedMove.SetOff(late = false), mine(OrderStatus._2, at(12, "13:10")))
        assertEquals(PinnedMove.SetOff(late = true), mine(OrderStatus._2, at(12, "14:10")))
    }

    @Test
    fun `on the way, slide to start`() {
        assertEquals(PinnedMove.Start, mine(OrderStatus._3, at(12, "13:40")))
    }

    @Test
    fun `solo, the after photo first, then complete`() {
        assertEquals(PinnedMove.TakeAfterPhoto, mine(OrderStatus._4, at(12, "15:12")))
        assertEquals(PinnedMove.Complete, mine(OrderStatus._4, at(12, "15:49")) { it.copy(afterPhoto = true) })
    }

    @Test
    fun `a card job Stripe has not charged gets the locked slide where Complete would be`() {
        assertEquals(PinnedMove.CompleteLocked, mine(OrderStatus._4, at(12, "15:49")) { it.copy(afterPhoto = true, charged = false) })
        assertEquals(
            PinnedMove.CompleteLocked,
            mine(OrderStatus._4, at(13, "17:50")) { it.copy(afterPhoto = true, charged = false, step = CompletionStep.CompleteJob) },
        )
    }

    @Test
    fun `cash, the collection pair, with the not-paid report only through its gate`() {
        assertEquals(PinnedMove.CollectCash(canReportNotPaid = true), mine(OrderStatus._4, at(16, "09:48")) { it.copy(afterPhoto = true, cash = true, canReport = true) })
        assertEquals(PinnedMove.CollectCash(canReportNotPaid = false), mine(OrderStatus._4, at(16, "09:48")) { it.copy(afterPhoto = true, cash = true) })
    }

    @Test
    fun `crew, I'm done once an after photo exists, the photo before it`() {
        assertEquals(PinnedMove.MarkMyPart, mine(OrderStatus._4, at(13, "17:46")) { it.copy(step = CompletionStep.MarkMyPart, afterPhoto = true) })
        assertEquals(PinnedMove.TakeAfterPhoto, mine(OrderStatus._4, at(13, "16:00")) { it.copy(step = CompletionStep.MarkMyPart) })
    }

    @Test
    fun `crew, once the crew-mate has marked, I'm done without an after photo, since marking a part needs none`() {
        assertEquals(PinnedMove.MarkMyPart, mine(OrderStatus._4, at(13, "17:46")) { it.copy(step = CompletionStep.MarkMyPart, mateMarked = true) })
        assertEquals(
            PinnedMove.TakeAfterPhoto,
            mine(OrderStatus._4, at(13, "17:50")) { it.copy(step = CompletionStep.CompleteJob, mateMarked = true) },
        )
    }

    @Test
    fun `crew waiting, Call the crew-mate while she has a phone, else the words`() {
        val lenka = CrewSeat(givenName = "Lenka", initials = "LP", doneAt = null, phone = "+420 605 318 772", isMe = false)
        assertEquals(
            PinnedMove.CallMate("Lenka", "+420 605 318 772"),
            mine(OrderStatus._4, at(13, "17:46")) { it.copy(step = CompletionStep.WaitingForCrew, afterPhoto = true, waitingFor = lenka) },
        )
        assertEquals(
            PinnedMove.WaitingFor("Lenka"),
            mine(OrderStatus._4, at(13, "17:46")) { it.copy(step = CompletionStep.WaitingForCrew, afterPhoto = true, waitingFor = lenka.copy(phone = null)) },
        )
    }

    @Test
    fun `crew, both parts marked, slide to complete`() {
        assertEquals(PinnedMove.Complete, mine(OrderStatus._4, at(13, "17:50")) { it.copy(step = CompletionStep.CompleteJob, afterPhoto = true) })
    }

    @Test
    fun `closed or not yet confirmed, nothing to do`() {
        assertEquals(PinnedMove.None, mine(OrderStatus._5, at(12, "16:20")))
        assertEquals(PinnedMove.None, mine(OrderStatus._6, at(12, "09:20")))
        assertEquals(PinnedMove.None, mine(OrderStatus._0, at(12, "09:20")))
        assertEquals(PinnedMove.None, notMine(OrderStatus._5, at(12, "09:20"), a1Start, 120))
    }

    private fun card(status: Int?, type: Int = 2) = OrderItem(paymentType = Code(value = type), paymentStatus = status?.let { Code(value = it) })

    @Test
    fun `a card job is charged once paid, refunded or partly refunded`() {
        listOf(2, 4, 6).forEach { assertTrue("status $it", card(it).cardCharged()) }
    }

    @Test
    fun `a card job pending, failed or disputed is not charged`() {
        listOf(1, 3, 5).forEach { assertFalse("status $it", card(it).cardCharged()) }
    }

    @Test
    fun `a cash job, or a status that did not arrive, is not held by the card gate`() {
        assertTrue(card(1, type = 1).cardCharged())
        assertTrue(card(null).cardCharged())
    }

    @Test
    fun `the overlap reason's words keep unbounded room when its parent asks its size`() {
        val unbounded = overlapReasonRoomPx(roomPx = Constraints.Infinity, lockWidthPx = 48, gapPx = 18)
        assertEquals(Constraints.Infinity, unbounded)
        assertEquals(Constraints.Infinity, Constraints(maxWidth = unbounded).maxWidth)
        assertEquals(900 - 48 - 18, overlapReasonRoomPx(roomPx = 900, lockWidthPx = 48, gapPx = 18))
        assertEquals(0, overlapReasonRoomPx(roomPx = 40, lockWidthPx = 48, gapPx = 18))
    }
}
