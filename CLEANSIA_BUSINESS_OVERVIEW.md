# Cleansia - Business Overview & Application Flow

**Last Updated**: 2025-12-20
**Version**: 1.1.0
**For**: Non-Technical Stakeholders

---

## Table of Contents

1. [What is Cleansia?](#what-is-cleansia)
2. [Who Uses Cleansia?](#who-uses-cleansia)
3. [Key Benefits](#key-benefits)
4. [Complete User Journey](#complete-user-journey)
5. [Main Features Explained](#main-features-explained)
6. [How Money Flows](#how-money-flows)
7. [Automated Processes](#automated-processes)
8. [Mobile Experience](#mobile-experience)
9. [Multilingual Support](#multilingual-support)
10. [System Status & Reliability](#system-status--reliability)

---

## What is Cleansia?

Cleansia is a **complete business management platform** for cleaning service companies. It handles everything from customer orders to employee payments, making your cleaning business run smoothly and efficiently.

Think of it as your digital business assistant that:
- Takes customer orders online
- Processes payments automatically
- Assigns work to employees
- Calculates employee pay
- Generates invoices and receipts
- Sends emails automatically
- Provides business insights through analytics

---

## Who Uses Cleansia?

### 1. **Customers** (Future - Not Yet Built)
Customers will be able to:
- Book cleaning services online
- Pay with credit card or cash
- Track their order status
- View before/after photos of completed work
- Download receipts
- Raise concerns if something goes wrong

### 2. **Employees** (Currently Available - Partner App)
Cleaning staff can:
- Register and create their profile
- See available cleaning jobs
- Accept jobs they want to work on
- Upload before/after photos of their work
- Mark jobs as complete
- View their earnings
- See their invoices and payment history

### 3. **Business Administrators** (Future - Admin App)
Company managers and owners will be able to:
- Monitor all orders and employees
- Approve employee registrations
- Review and approve invoices
- Process employee payments
- Handle customer complaints
- View detailed business reports
- Manage services and pricing

---

## Key Benefits

### For the Business
✅ **Automated Payroll** - No more manual calculations, everything is computed automatically
✅ **Real-Time Tracking** - Know exactly what's happening with every order
✅ **Professional Documentation** - Automatic receipts and invoices in multiple languages
✅ **Better Cash Flow** - Instant payment processing with credit cards
✅ **Data-Driven Decisions** - Analytics show what's working and what's not
✅ **Less Admin Work** - System handles repetitive tasks automatically

### For Employees
✅ **Transparent Earnings** - See exactly how much you'll earn from each job
✅ **Flexible Work** - Choose which jobs to accept
✅ **Easy Documentation** - Upload photos directly from your phone
✅ **Clear Payment History** - Access all your invoices anytime
✅ **Works on Mobile** - Use it anywhere on your smartphone

### For Customers
✅ **Easy Booking** - Order cleaning services in minutes
✅ **Secure Payments** - Pay safely with credit card
✅ **Quality Assurance** - See photos of completed work
✅ **Email Confirmations** - Get receipts automatically
✅ **Issue Resolution** - Easy way to report problems

---

## Complete User Journey

### Step 1: Customer Books a Service

**What Happens:**
1. Customer visits your website or app
2. They select:
   - Type of cleaning service (basic, deep clean, etc.)
   - Number of rooms and bathrooms
   - Date and time
   - Any extra services (window cleaning, carpet cleaning, etc.)
3. System automatically calculates the price
4. Customer enters their address and contact details
5. They choose how to pay: **Credit Card** or **Cash**

**Behind the Scenes:**
- System calculates how many employees are needed
- Estimates how long the job will take
- Creates a unique order number
- Generates a confirmation code

### Step 2A: Credit Card Payment

**What Happens:**
1. Customer is sent to Stripe (secure payment processor)
2. They enter their card details
3. Payment is processed immediately

**Behind the Scenes:**
- System creates a secure payment session
- Stripe processes the payment
- Once confirmed, Stripe notifies our system
- Order is marked as "Paid"
- Receipt is automatically generated as a PDF
- Receipt is emailed to the customer

### Step 2B: Cash Payment

**What Happens:**
1. Order is created immediately
2. Payment will be collected when the job is done

**Behind the Scenes:**
- Order is marked as "Paid" (pending cash collection)
- Receipt is generated and emailed to customer
- Employee will collect cash during the visit

### Step 3: Employee Assignment

**What Happens (Currently):**
1. Employees see the new order in their app
2. They can view all the details:
   - Location
   - Type of cleaning
   - How much they'll earn
   - When it's scheduled
3. Employee clicks "Take Order" to accept it
4. Order status changes to "In Progress"

**What Will Happen (Future with Admin App):**
- Admin can assign specific employees to orders
- System can suggest best employees based on:
  - Location (who lives closest)
  - Availability
  - Performance history

### Step 4: Completing the Job

**What Happens:**
1. Employee arrives at the location
2. Before starting, they take "before" photos using their phone
3. They complete the cleaning work
4. After finishing, they take "after" photos
5. They click "Complete Order" in the app
6. They write any notes about the job
7. They record the actual time spent

**Behind the Scenes:**
- All photos are stored securely in cloud storage
- System calculates the final employee payment based on:
  - Services performed
  - Extras completed
  - Travel distance
  - Any bonuses or deductions
- Order status changes to "Completed"
- Another receipt is generated (if needed)
- Customer is notified via email

### Step 5: Employee Payment Cycle

**What Happens:**
Cleansia operates on **bi-weekly pay periods** (every 2 weeks):

**Week 1-2:** Employees work on various jobs
- Each completed job adds to their earnings
- They can see their running total in the app

**End of Week 2 (Automatic):**
- System automatically closes the pay period at 2 AM
- For each employee, the system:
  - Finds all their completed jobs
  - Calculates total earnings
  - Adds any bonuses (given by admin)
  - Subtracts any deductions (given by admin)
  - Generates a professional invoice PDF
  - Emails the invoice to the employee

**Week 3 (Admin Action - Future):**
- Admin reviews all invoices
- Approves them (or makes adjustments if needed)
- Marks invoices as "Paid" when money is transferred
- Employee receives payment notification

**Special Features:**
- Each invoice has a unique "Variable Symbol" for Czech bank transfers
- This allows automatic payment matching in banking systems
- Invoices can be cancelled by admin if there's an error (with reason tracking)

---

## Main Features Explained

### 1. Order Management

**Simple Explanation:** Complete control over every cleaning job from start to finish.

**What You Can Do:**
- Create new orders manually (if customer calls)
- See all orders in one place
- Filter orders by:
  - Customer name, email, or phone
  - Order status (pending, completed, etc.)
  - Date range
  - Price range
- View complete order details including:
  - What services were ordered
  - Who's assigned to do it
  - Payment status
  - Before/after photos
  - Complete history of status changes

**Real-World Benefit:** Never lose track of an order. Find any job instantly using search.

### 2. Payment Processing

**Simple Explanation:** Get paid faster and more securely.

**Credit Card Payments:**
- Customers pay immediately online
- Money arrives in your bank account within days
- No risk of unpaid bills
- Professional checkout experience
- Automatic fraud protection

**Cash Payments:**
- Tracked in the system
- Employee collects cash during visit
- Still generates professional receipts

**Real-World Benefit:** Improve cash flow, reduce payment delays, look more professional.

### 3. Employee Payroll System

**Simple Explanation:** Automated, transparent, and accurate employee payments.

**How Employee Pay is Calculated:**

Each employee has their own pay configuration:
- **Base Pay**: Amount per service or package
- **Extra Services Pay**: Additional money for window cleaning, carpet cleaning, etc.
- **Travel Compensation**: Money per kilometer driven
- **Minimum Pay**: Guaranteed minimum per job
- **Maximum Pay**: Cap to prevent overpayment

**Example Calculation:**
```
Employee does a job:
- 1x Basic Cleaning (500 CZK)
- 1x Window Cleaning (200 CZK)
- Traveled 10 km (50 CZK)
Total: 750 CZK

Admin adds:
+ 100 CZK bonus (great work!)
- 50 CZK deduction (broke a plate)

Final Pay: 800 CZK
```

**Pay Period Process:**
1. Period opens (January 1-14)
2. Employees work and see earnings accumulate
3. Period auto-closes on January 15 at 2 AM
4. Invoices are generated and emailed automatically
5. Admin reviews and approves
6. Admin processes payments
7. New period opens automatically (January 15-28)

**Real-World Benefit:** No more Excel spreadsheets, no calculation errors, complete transparency.

### 4. Photo Management

**Simple Explanation:** Visual proof of work quality.

**What It Does:**
- Employees upload photos from their phone
- Two types: "Before Service" and "After Service"
- Photos are stored permanently in cloud
- Can be viewed in a gallery
- Can be downloaded anytime
- Can be deleted if needed

**Real-World Benefit:**
- Prove quality of work to customers
- Resolve disputes easily
- Train new employees with examples
- Marketing material for website

### 5. Professional Documents (PDFs)

**Simple Explanation:** Automatic generation of invoices and receipts.

**What Gets Generated:**

**For Customers - Receipts:**
- Order confirmation receipt
- Payment receipt after completion
- Includes:
  - Receipt number
  - Company branding
  - Services provided
  - Total amount
  - QR code (optional)

**For Employees - Invoices:**
- Professional payroll invoices
- Includes:
  - Invoice number
  - Variable symbol (for Czech banking)
  - All jobs completed in the period
  - Detailed breakdown of earnings
  - Bonuses and deductions
  - Total amount
  - Company information

**Multilingual:**
- Documents in Czech and English
- Matches the user's preferred language
- Professional translations

**Real-World Benefit:** Save hours of manual work, look professional, comply with regulations.

### 6. Email System

**Simple Explanation:** Automatic emails for every important event.

**What Emails Are Sent:**

| When | Who Gets It | What It Says |
|------|-------------|--------------|
| Registration | New employee | "Welcome! Please confirm your email" |
| Password Reset | Any user | "Click here to reset your password" |
| Order Created | Customer | "Order confirmed! Here's your receipt" |
| Payment Received | Customer | "Payment successful! Order number XXX" |
| Period Closes | All employees | "Pay period closed. Your invoice is attached" |
| Period Ending Soon | All employees | "3 days until period ends. Check your hours" |

**Languages:** All emails in Czech and English

**Real-World Benefit:** Keep everyone informed automatically, no manual follow-ups needed.

### 7. Dashboard & Analytics

**Simple Explanation:** See how your business is performing at a glance.

**What You Can See:**

**Overview Statistics:**
- Total orders this month
- Total revenue
- Number of active employees
- Average order value
- Average completion time

**Charts & Graphs:**
- Revenue trends over time
- Orders by status (completed, pending, etc.)
- Orders by service type
- Employee productivity rankings
- Time spent per order

**Filter by Date:**
- View any time period
- Compare month to month
- Identify busy seasons

**Real-World Benefit:** Make data-driven decisions, spot problems early, plan better.

### 8. Dispute Management (Backend Ready)

**Simple Explanation:** Handle customer complaints systematically.

**How It Works:**

**Customer Side (Future):**
1. Customer notices a problem
2. They create a dispute through the app
3. They select the type:
   - Want a refund
   - Credit card chargeback
   - Service quality issue
   - Billing error
   - Other
4. They describe the problem
5. They say what they want (refund, re-do, etc.)
6. They can attach photos as evidence

**Admin Side (Future):**
1. Sees all disputes in one place
2. Can update status:
   - Open (just created)
   - In Review (investigating)
   - Resolved (fixed)
   - Closed (completed)
   - Escalated (needs management attention)
3. Add notes about the resolution
4. Track everything in the system

**Real-World Benefit:**
- Professional complaint handling
- Nothing gets forgotten
- Learn from problems
- Improve service quality

---

## How Money Flows

### Customer → Business

```
Customer Places Order
        ↓
    Credit Card?
    ├── Yes → Stripe processes → Money in bank (2-3 days)
    └── No  → Cash on completion → Employee collects
        ↓
   System Tracks Everything
        ↓
   Receipt Generated & Emailed
```

### Business → Employee

```
Employee Completes Jobs
        ↓
   System Calculates Pay
   (base + extras + travel)
        ↓
   Every 2 Weeks: Period Closes
        ↓
   Invoice Generated Automatically
        ↓
   Admin Reviews & Approves
        ↓
   Admin Processes Payment
        ↓
   Employee Receives Money
        ↓
   Invoice Marked as "Paid"
```

### Money Tracking

**Every transaction is tracked:**
- Who paid
- How much
- When
- For what service
- Payment method
- Receipt/Invoice number

**Reports Available:**
- Total revenue by period
- Revenue by service type
- Employee earnings by period
- Pending payments
- Payment history

---

## Automated Processes

### What Runs Automatically (No Human Action Needed)

#### 1. **Pay Period Closure** (Daily at 2 AM)

**What It Does:**
- Checks if any pay periods have ended
- For each ended period:
  - Closes it automatically
  - Finds all employees who worked
  - For each employee:
    - Collects all their completed jobs
    - Calculates total earnings
    - Generates invoice PDF
    - Emails invoice to employee
  - Creates next pay period automatically

**Why It's Important:**
- Never forget to close a period
- Employees get invoices immediately
- No manual work required
- Happens at 2 AM so no one is affected

#### 2. **Pay Period Reminder** (Daily at 9 AM)

**What It Does:**
- Checks if period ends in 3 days or less
- If yes:
  - Sends reminder email to all employees
  - Email says: "Period ends soon! Verify your hours"

**Why It's Important:**
- Employees can review their earnings
- Catch any errors before period closes
- Time to complete pending jobs

#### 3. **Payment Confirmation** (Real-time)

**What It Does:**
- Stripe sends notification when payment succeeds
- System immediately:
  - Marks order as "Paid"
  - Generates receipt PDF
  - Emails receipt to customer
  - Makes order available for employees

**Why It's Important:**
- Instant confirmation
- No delays
- Professional customer experience

#### 4. **Health Monitoring** (Continuous)

**What It Does:**
- Constantly checks if everything is working:
  - Database connection
  - File storage
  - Email service
  - Payment processor
- If something breaks:
  - System knows immediately
  - Admin can be alerted

**Why It's Important:**
- Catch problems before customers do
- Minimize downtime
- Maintain reliability

---

## Mobile Experience

### Why Mobile Matters

Most employees use their phones, not computers. Cleansia is fully optimized for mobile devices.

### Mobile Features

**Responsive Design:**
- Everything adjusts to phone screen size
- Buttons are finger-friendly
- Easy to tap and navigate
- No zooming needed

**Mobile Menu:**
- Hamburger menu (three lines icon)
- Sidebar slides in smoothly
- Tap anywhere to close
- Works great with one hand

**Photo Upload:**
- Use phone camera directly
- Take photos on-site
- Upload immediately
- No need to transfer from camera

**Touch-Friendly:**
- All buttons big enough to tap
- Swipe gestures work
- No tiny clickable areas
- Smooth animations

**Works Everywhere:**
- At customer's home (taking photos)
- On the bus (viewing jobs)
- At home (checking earnings)
- Anywhere with internet

---

## Multilingual Support

### Supported Languages

Currently: **Czech** and **English**

### What's Translated

**User Interface:**
- All buttons and labels
- Menu items
- Form fields
- Error messages
- Success messages

**Emails:**
- Email confirmation
- Password reset
- Order receipts
- Pay period closed
- Pay period reminder

**Documents (PDFs):**
- Invoices
- Receipts
- All text content

### How It Works

**For Users:**
1. During registration, select preferred language
2. Everything appears in that language
3. Can change language in profile settings
4. Choice is remembered

**For Business:**
- Attract international customers
- Employ non-Czech speakers
- Professional in both languages
- No manual translation needed

---

## System Status & Reliability

### Current Status: 100% Complete (Partner App) ✅

**What's Working Now:**
- ✅ Employee registration and login
- ✅ Order creation and management
- ✅ Payment processing (Stripe + Cash)
- ✅ Photo upload and management
- ✅ Automated payroll calculations
- ✅ Invoice generation
- ✅ Email notifications
- ✅ Dashboard analytics
- ✅ Mobile responsive interface
- ✅ Health monitoring
- ✅ Request logging (for troubleshooting)

**What's Coming Next:**
- ⏳ Customer-facing app (for customers to book services)
- ⏳ Admin app (for business owners to manage everything)
- ⏳ Dispute resolution interface
- ⏳ Advanced reporting
- ⏳ Employee performance tracking

### Reliability Features

**Automatic Backups:**
- All data backed up regularly
- Photos stored in secure cloud storage
- Can recover from any failure

**Error Handling:**
- If credit card payment fails, customer is notified
- If invoice generation fails, it's retried automatically
- If email fails, system tries 3 times with delays

**Logging:**
- Every action is logged
- Easy to troubleshoot problems
- Track what happened when

**Security:**
- All passwords encrypted
- HTTPS encryption for all data
- Secure payment processing
- Email confirmation required
- Strong password policy (12+ characters, special characters)

**Performance:**
- Fast response times
- Optimized database queries
- Efficient photo storage
- No lag on mobile devices

---

## What Makes Cleansia Special?

### 1. **Completely Automated Payroll**
Most cleaning companies still use Excel. Cleansia calculates everything automatically, saving hours of work every week.

### 2. **Visual Quality Proof**
Before/after photos show customers the value they received and protect you from false complaints.

### 3. **Instant Payments**
Credit card integration means money in your bank within days, not weeks.

### 4. **Transparent for Employees**
Employees see exactly how much they'll earn before accepting a job. No surprises, no disputes.

### 5. **Professional Documents**
Automatic invoices and receipts in multiple languages make you look established and trustworthy.

### 6. **Mobile-First**
Built for people who work on their phones, not at desks.

### 7. **Multilingual**
Serve both local and international markets without extra work.

### 8. **Scalable**
Works for 5 employees or 500. System doesn't slow down as you grow.

---

## Real-World Scenarios

### Scenario 1: New Customer Order

**Customer Experience:**
- 10:00 AM - Books cleaning on website (3 hours)
- 10:01 AM - Pays with credit card (1 minute)
- 10:02 AM - Receives order confirmation email with receipt

**Employee Experience:**
- 10:03 AM - Sees new order in app
- 10:15 AM - Accepts the order
- 10:16 AM - Customer is notified "Employee assigned!"

**Business Experience:**
- 10:02 AM - Order tracked in system
- 10:02 AM - Payment confirmed by Stripe
- Money in bank in 2-3 days
- Zero manual work needed

---

### Scenario 2: Completing a Job

**Employee Experience:**
1. Arrives at customer's home
2. Opens app, takes 3 "before" photos
3. Completes cleaning (2 hours)
4. Takes 3 "after" photos
5. Clicks "Complete Order"
6. Writes note: "All tasks completed. Windows were very dirty, took extra time"
7. Enters actual time: 120 minutes

**System:**
- Calculates pay: Base (500 CZK) + Windows (200 CZK) + Travel (50 CZK) = 750 CZK
- Shows employee: "You earned 750 CZK"
- Updates order status to "Completed"
- Notifies customer via email with before/after photos attached

**Customer:**
- Receives email: "Your cleaning is complete!"
- Views photos in email
- Downloads receipt PDF

---

### Scenario 3: Pay Period End

**Day 14 at 11:59 PM:**
- Employee has completed 15 jobs this period
- Total earnings: 11,250 CZK

**Day 15 at 2:00 AM (Automatic):**
- System closes the period
- Finds all of employee's jobs
- Calculates total: 11,250 CZK
- Admin had added 500 CZK bonus for great reviews
- Final total: 11,750 CZK
- Generates invoice PDF
- Emails invoice to employee

**Day 15 at 8:00 AM:**
- Employee wakes up
- Sees invoice in email
- Downloads it
- Knows exactly what they earned

**Day 20 (Admin):**
- Admin reviews invoice
- Clicks "Approve"
- Processes bank transfer
- Clicks "Mark as Paid"
- Employee receives notification: "Payment sent!"

---

### Scenario 4: Customer Complaint (Future)

**Customer:**
- Not happy with cleaning quality
- Opens dispute in app
- Selects: "Service Quality Issue"
- Writes: "Bathroom not cleaned properly"
- Attaches photo of missed spot
- Requests: "Re-clean bathroom"

**System:**
- Creates dispute record
- Sends notification to admin
- Tracks status

**Admin (Future):**
- Sees dispute in dashboard
- Changes status to "In Review"
- Contacts employee for their side
- Contacts customer
- Decides: Send employee back to re-clean
- Changes status to "Resolved"
- Adds note: "Employee returned, re-cleaned bathroom. Customer satisfied"
- Closes dispute

**Result:**
- Problem solved professionally
- Everything documented
- Customer feels heard
- Business learns what to improve

---

## Common Questions

### "Can employees see other employees' earnings?"
No. Each employee only sees their own orders, earnings, and invoices. Complete privacy.

### "What if an employee uploads wrong photos?"
They can delete photos and upload new ones. Only photos uploaded by employees for their assigned orders.

### "What happens if payment fails?"
Customer sees error message and can try again. Order stays in "pending" status. They receive email with payment link to try again later.

### "Can we change employee pay rates?"
Yes. Admin can update pay configurations anytime. Changes apply to new jobs, not completed ones.

### "What if we need to cancel an invoice?"
Admin can cancel invoices with a reason. System tracks who cancelled, when, and why. Cannot cancel invoices that are already paid.

### "How do we handle tips?"
Tips can be added as bonuses during invoice review. Admin adds the amount before approving the invoice.

### "What if employee forgets to complete an order?"
Order stays in "In Progress" status. Admin can see all incomplete orders and follow up. Employee still sees it in their app.

### "Can we customize the services we offer?"
Yes. Admin app (future) will allow managing services, packages, and pricing.

### "What reports can we generate?"
Currently: Dashboard with key metrics. Future admin app will have extensive reports (revenue by service, employee performance, customer trends, etc.).

### "Is customer data secure?"
Yes. All data encrypted, secure cloud storage, HTTPS everywhere, compliant with data protection regulations.

---

## Success Metrics

### How to Measure Cleansia's Impact

**Operational Efficiency:**
- ⏱️ Time spent on payroll: Excel (4-5 hours/period) → Cleansia (30 minutes review)
- 📊 Invoicing errors: Manual (5-10%) → Cleansia (0%)
- 💰 Payment delays: Manual (1-2 weeks) → Cleansia (same day after approval)

**Business Growth:**
- 📈 Order tracking: Manual (messy) → Cleansia (100% visibility)
- 💳 Payment collection: Cash (risky) → Credit card (secure, instant)
- 🎯 Customer satisfaction: Unknown → Tracked via disputes and photos

**Employee Satisfaction:**
- ✅ Pay transparency: Unclear → Crystal clear
- 📱 Work flexibility: Call to get jobs → See and choose in app
- 💰 Payment speed: Irregular → Bi-weekly guaranteed

---

## Next Steps for Your Business

### Phase 1: Partner App (✅ Complete)
You can start using this NOW for:
- Employee management
- Order tracking
- Payment processing
- Automated payroll

### Phase 2: Customer App (⏳ Coming)
When ready, customers can:
- Book online 24/7
- Pay instantly
- Track their orders
- Rate services

### Phase 3: Admin App (⏳ Coming)
Complete business control:
- Employee approval
- Financial reports
- Service management
- Dispute resolution
- Full analytics

---

## Summary

Cleansia transforms a cleaning business from manual spreadsheets and phone calls to a professional, automated platform. It saves time, reduces errors, improves customer experience, and makes employees happier.

The system works 24/7, handles payments automatically, generates professional documents, sends emails, calculates payroll, and provides insights—all without manual work.

Whether you have 5 employees or 50, Cleansia scales with your business and makes operations smooth and professional.

---

**Questions?**
Contact: support@cleansia.com
Documentation: See CLEANSIA_PROJECT_DOCUMENTATION.md for technical details

---

**Version**: 1.1.0
**Last Updated**: 2025-12-20
**Status**: Partner App Complete & Ready for Use ✅
