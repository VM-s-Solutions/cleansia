import CleansiaPartnerApi
import Foundation

extension L10n {
    enum Orders {
        static var appleMaps: String {
            localized("orders_maps_apple")
        }

        static var googleMaps: String {
            localized("orders_maps_google")
        }

        static var title: String {
            localized("orders")
        }

        static var removalTitle: String {
            localized("order_removal_title")
        }

        static var available: String {
            localized("available")
        }

        static var active: String {
            localized("active")
        }

        static var history: String {
            localized("history")
        }

        static var completed: String {
            localized("completed")
        }

        static var takeOrder: String {
            localized("take_order")
        }

        static var youEarn: String {
            localized("you_earn")
        }

        static var noActiveOrders: String {
            localized("no_active_orders")
        }

        static var sortEarningsHighToLow: String {
            localized("sort_earnings_high_to_low")
        }

        static var sortSoonestFirst: String {
            localized("sort_soonest_first")
        }

        static var periodThisWeek: String {
            localized("period_this_week")
        }

        static var dayToday: String {
            localized("day_today")
        }

        static var dayTomorrow: String {
            localized("day_tomorrow")
        }

        static var dayLater: String {
            localized("day_later")
        }

        static func rooms(_ count: Int) -> String {
            plural("scope_rooms", count)
        }

        static func baths(_ count: Int) -> String {
            plural("scope_baths", count)
        }

        static func crewSize(_ count: Int) -> String {
            plural("crew_size", count)
        }

        static var unscheduled: String {
            localized("unscheduled")
        }

        // Detail

        static var actionCall: String {
            localized("action_call")
        }

        static var actionSms: String {
            localized("action_sms")
        }

        static var actionNavigate: String {
            localized("action_navigate")
        }

        static var customerDetailsClosedCompleted: String {
            localized("order_customer_details_closed_completed")
        }

        static var customerDetailsClosedCancelled: String {
            localized("order_customer_details_closed_cancelled")
        }

        static var scopeServicesLabel: String {
            localized("scope_services_label")
        }

        static var scopeExtrasLabel: String {
            localized("scope_extras_label")
        }

        /// The cleaner retypes these into a keypad at a door, so both blocks are copyable. Neutral keys
        /// rather than the invoice ones: the values match, but `invoice_field_copy` would lie here.
        static var copyInstruction: String {
            localized("instructions_copy")
        }

        static var copiedInstruction: String {
            localized("instructions_copied")
        }

        static var accessSectionTitle: String {
            localized("access_section_title")
        }

        static var fromCustomerSectionTitle: String {
            localized("from_customer_section_title")
        }

        static var noteGeneralLabel: String {
            localized("note_general_label")
        }

        static var noteSpecialLabel: String {
            localized("note_special_label")
        }

        static var paymentSectionTitle: String {
            localized("payment_section_title")
        }

        static var photosSectionTitle: String {
            localized("photos")
        }

        static var photosBefore: String {
            localized("before")
        }

        static var photosAfter: String {
            localized("after")
        }

        static var addPhoto: String {
            localized("add_photo")
        }

        static var deletePhoto: String {
            localized("delete_photo")
        }

        static var deletePhotoConfirmation: String {
            localized("delete_photo_confirmation")
        }

        static var cameraUnavailable: String {
            localized("camera_unavailable")
        }

        static var jobPhotosCameraOnly: String {
            localized("job_photos_camera_only")
        }

        static var photoEncodeFailed: String {
            localized("photo_encode_failed")
        }

        static var cameraPermissionTitle: String {
            localized("camera_permission_title")
        }

        static var cameraPermissionMessage: String {
            localized("camera_permission_message")
        }

        static var openSettings: String {
            localized("open_settings")
        }

        // MARK: - Timer hero

        static var trackerHeadlineDone: String {
            localized("tracker_headline_done")
        }

        static func statusLabel(_ status: OrderStatus?) -> String {
            switch status {
            case ._0: localized("status_new")
            case ._1: localized("status_pending")
            case ._2: localized("status_confirmed")
            case ._3: localized("status_on_the_way")
            case ._4: localized("status_in_progress")
            case ._5: localized("status_completed")
            case ._6: localized("status_cancelled")
            case .none: "—"
            }
        }

        /// A normal home says nothing: only the increased and the heavy level are named.
        static func dirtinessLevel(_ level: DirtinessLevel?) -> String? {
            switch level {
            case ._1: localized("dirtiness_level_increased")
            case ._2: localized("dirtiness_level_heavy")
            case ._0, .none: nil
            }
        }
    }
}

// MARK: - Board (the Orders tabs)

extension L10n.Orders {
    static func jobCount(_ count: Int) -> String {
        L10n.plural("orders_job_count", count)
    }

    static var opensTheJob: String {
        L10n.localized("orders_row_opens_job")
    }

    static var exactAddressAfterTake: String {
        L10n.localized("orders_map_exact_after_take")
    }

    static var mapRecentre: String {
        L10n.localized("orders_map_recenter")
    }

    static var yourShare: String {
        L10n.localized("orders_card_your_share")
    }

    static func durationH(_ hours: Int) -> String {
        L10n.format("orders_duration_h", hours)
    }

    static func durationHMin(_ hours: Int, _ minutes: Int) -> String {
        L10n.format("orders_duration_h_min", hours, minutes)
    }

    static func durationMin(_ minutes: Int) -> String {
        L10n.format("orders_duration_min", minutes)
    }

    static func crewTaken(_ count: Int) -> String {
        L10n.plural("orders_flag_crew_taken", count)
    }

    static var crewNobody: String {
        L10n.localized("orders_flag_crew_nobody")
    }

    static func flagTakeUntil(_ until: String) -> String {
        L10n.format("orders_flag_take_until", until)
    }

    static func flagOverlap(_ window: String) -> String {
        L10n.format("orders_flag_overlap", window)
    }

    static var extraShortOven: String {
        L10n.localized("extra_short_inside_oven")
    }

    static var extraShortFridge: String {
        L10n.localized("extra_short_inside_fridge")
    }

    static var extraShortInteriorWindows: String {
        L10n.localized("extra_short_interior_windows")
    }

    static var extraShortIroning: String {
        L10n.localized("extra_short_laundry_ironing")
    }

    static func overlapCount(_ count: Int) -> String {
        L10n.plural("available_overlap_count", count)
    }

    /// The banded straight-line distance from the cleaner's stored home (D2); the set is frozen.
    static func distanceBand(_ band: HomeDistanceBand) -> String {
        switch band {
        case ._1: L10n.localized("distance_band_under_3")
        case ._2: L10n.localized("distance_band_3_6")
        case ._3: L10n.localized("distance_band_6_10")
        case ._4: L10n.localized("distance_band_10_20")
        case ._5: L10n.localized("distance_band_over_20")
        }
    }

    static var distanceFootnote: String {
        L10n.localized("available_distance_footnote")
    }

    static var reservedThen: String {
        L10n.localized("available_reserved_then")
    }

    static func showingOf(_ shown: Int, _ total: Int) -> String {
        L10n.format("available_showing_of", shown, total)
    }

    static var loadMore: String {
        L10n.localized("available_load_more")
    }

    static var emptyTitle: String {
        L10n.localized("available_empty_title")
    }

    static var emptySubtitle: String {
        L10n.localized("available_empty_subtitle")
    }

    static var loadError: String {
        L10n.localized("orders_load_error")
    }

    static func staleSince(_ when: String) -> String {
        L10n.format("orders_stale_since", when)
    }

    static var loadMoreFailed: String {
        L10n.localized("orders_load_more_failed")
    }

    static var checkAgain: String {
        L10n.localized("orders_check_again")
    }

    static func sortA11y(_ label: String) -> String {
        L10n.format("orders_sort_a11y", label)
    }

    static var cashHidden: String {
        L10n.localized("available_cash_hidden")
    }

    static func heroIn(_ duration: String) -> String {
        L10n.format("active_hero_in", duration)
    }

    static func heroDue(_ time: String) -> String {
        L10n.format("active_hero_due", time)
    }

    static func heroUntil(_ time: String) -> String {
        L10n.format("active_hero_until", time)
    }

    static func setOffFrom(_ time: String) -> String {
        L10n.format("active_set_off_from", time)
    }

    static var heroDetails: String {
        L10n.localized("active_hero_details")
    }

    static func heroTeam(_ count: Int) -> String {
        L10n.plural("active_hero_team", count)
    }

    static var openJob: String {
        L10n.localized("orders_open_job")
    }

    static var rewardsTitle: String {
        L10n.localized("history_rewards_title")
    }

    static func historySummary(_ jobs: String, _ hours: String) -> String {
        L10n.format("history_summary", jobs, hours)
    }

    static func payoutsLink(_ section: String) -> String {
        L10n.format("history_payouts_link", section)
    }
}
