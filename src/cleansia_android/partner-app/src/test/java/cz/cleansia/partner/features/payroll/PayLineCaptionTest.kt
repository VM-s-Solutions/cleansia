package cz.cleansia.partner.features.payroll

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.PayLineType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PayLineCaptionTest {

    @Test
    fun `a job line needs no caption`() {
        assertNull(payLineCaptionRes(PayLineType._0))
    }

    @Test
    fun `each line for a job that did not happen says why`() {
        assertEquals(R.string.period_pay_line_cancellation_fee_share, payLineCaptionRes(PayLineType._1))
        assertEquals(R.string.period_pay_line_lockout_fee_share, payLineCaptionRes(PayLineType._2))
    }
}
