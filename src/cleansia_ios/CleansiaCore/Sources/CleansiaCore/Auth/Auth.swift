import Foundation

public struct JwtTokenResponseDto: Decodable, Sendable {
    public let token: String?
    public let isEmailConfirmed: Bool?
    public let hasAdminAccess: Bool?
    public let userId: String?
    public let email: String?
    public let refreshToken: String?
    public let refreshTokenExpiresAt: String?
}

public struct LoginRequest: Encodable, Sendable {
    public let email: String
    public let password: String
    public let rememberMe: Bool

    public init(email: String, password: String, rememberMe: Bool = true) {
        self.email = email
        self.password = password
        self.rememberMe = rememberMe
    }
}

public struct RefreshTokenRequest: Encodable, Sendable {
    public let token: String
    public init(token: String) {
        self.token = token
    }
}

public struct LogoutRequest: Encodable, Sendable {
    public let token: String
    public init(token: String) {
        self.token = token
    }
}

public struct RegisterEmployeeRequest: Encodable, Sendable {
    public let email: String
    public let password: String
    public let firstName: String
    public let lastName: String
    public let language: String
}

public struct ConfirmUserEmailRequest: Encodable, Sendable {
    public let code: String
}

public struct ResendConfirmationEmailRequest: Encodable, Sendable {
    public let email: String
    public let language: String
}

public struct ForgotPasswordRequest: Encodable, Sendable {
    public let email: String
    public let language: String
}

public enum LoginOutcome: Equatable, Sendable {
    case authenticated
    case unverifiedEmail(email: String, hasToken: Bool)
}

public final class AuthApiClient: AuthSpine, @unchecked Sendable {
    private let apiBaseURL: URL
    private let authedSession: URLSession
    private let noAuthSession: URLSession
    private let headerAdapter: HeaderAdapter
    public let tokenStore: TokenStore
    private let sessionScopedCaches: SessionScopedCacheRegistry

    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    public init(
        apiBaseURL: URL,
        tokenStore: TokenStore,
        headerAdapter: HeaderAdapter,
        sessionScopedCaches: SessionScopedCacheRegistry,
        authedSession: URLSession = .shared,
        noAuthSession: URLSession = URLSession(configuration: .ephemeral)
    ) {
        self.apiBaseURL = apiBaseURL
        self.tokenStore = tokenStore
        self.headerAdapter = headerAdapter
        self.sessionScopedCaches = sessionScopedCaches
        self.authedSession = authedSession
        self.noAuthSession = noAuthSession
    }

    public func login(email: String, password: String, rememberMe: Bool = true) async -> ApiResult<LoginOutcome> {
        let request = LoginRequest(email: email, password: password, rememberMe: rememberMe)
        let result: ApiResult<JwtTokenResponseDto> = await post(
            path: "api/Auth/Login",
            body: request,
            useNoAuthSession: true
        )

        switch result {
        case let .failure(error):
            return .failure(error)
        case let .success(dto):
            return .success(resolveEmailGate(
                dto,
                fallbackEmail: email,
                refreshLifetime: rememberMe ? .longLived : .shortLived
            ))
        }
    }

    public func register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String
    ) async -> ApiResult<Bool> {
        let body = RegisterEmployeeRequest(
            email: email,
            password: password,
            firstName: firstName,
            lastName: lastName,
            language: language
        )
        return await post(path: "api/Auth/RegisterEmployee", body: body, useNoAuthSession: true)
    }

    public func confirmEmail(code: String) async -> ApiResult<LoginOutcome> {
        let result: ApiResult<JwtTokenResponseDto> = await post(
            path: "api/Auth/ConfirmUserEmail",
            body: ConfirmUserEmailRequest(code: code),
            useNoAuthSession: false,
            method: .put
        )
        switch result {
        case let .failure(error):
            return .failure(error)
        case let .success(dto):
            return .success(resolveEmailGate(dto, fallbackEmail: dto.email ?? "", refreshLifetime: .shortLived))
        }
    }

    public func resendConfirmation(email: String, language: String) async -> ApiResult<Bool> {
        let body = ResendConfirmationEmailRequest(email: email, language: language)
        return await post(
            path: "api/Auth/ResendConfirmationEmail",
            body: body,
            useNoAuthSession: true
        )
    }

    public func forgotPassword(email: String, language: String) async -> ApiResult<Void> {
        let body = ForgotPasswordRequest(email: email, language: language)
        let result = await send(path: "api/Auth/ForgotPassword", body: body, useNoAuthSession: true, method: .post)
        switch result {
        case let .failure(error):
            return .failure(error)
        case .success:
            return .success(())
        }
    }

    private func resolveEmailGate(
        _ dto: JwtTokenResponseDto,
        fallbackEmail: String,
        refreshLifetime: RefreshLifetime
    ) -> LoginOutcome {
        guard let token = dto.token, !token.isEmpty else {
            return .unverifiedEmail(email: fallbackEmail, hasToken: false)
        }
        persist(dto, fallbackRefreshLifetime: refreshLifetime)
        if dto.isEmailConfirmed != true {
            return .unverifiedEmail(email: fallbackEmail, hasToken: true)
        }
        return .authenticated
    }

    public func logout() async {
        if let refreshToken = tokenStore.current()?.refreshToken, !refreshToken.isEmpty {
            await postExpectingNoBody(path: "api/Auth/Logout", body: LogoutRequest(token: refreshToken))
        }
        await signOutLocal()
    }

    public func signOutLocal() async {
        tokenStore.clear()
        await sessionScopedCaches.clearAll()
    }

    public func refresh(refreshToken: String) async -> RefreshedTokens? {
        let body = RefreshTokenRequest(token: refreshToken)
        let result: ApiResult<JwtTokenResponseDto> = await post(
            path: "api/Auth/RefreshToken",
            body: body,
            useNoAuthSession: true
        )
        guard case let .success(dto) = result, let access = dto.token, !access.isEmpty,
              let rotatedRefresh = dto.refreshToken, !rotatedRefresh.isEmpty
        else {
            return nil
        }
        return RefreshedTokens(
            accessToken: access,
            accessTokenExpiresAt: accessExpiry(from: access),
            refreshToken: rotatedRefresh,
            refreshTokenExpiresAt: refreshExpiry(from: dto.refreshTokenExpiresAt, lifetime: .shortLived)
        )
    }

    private func post<Response: Decodable>(
        path: String,
        body: some Encodable,
        useNoAuthSession: Bool,
        method: HttpMethod = .post
    ) async -> ApiResult<Response> {
        let result = await send(path: path, body: body, useNoAuthSession: useNoAuthSession, method: method)
        switch result {
        case let .failure(error):
            return .failure(error)
        case let .success(payload):
            do {
                return try .success(decoder.decode(Response.self, from: payload.data))
            } catch {
                return .failure(ApiError(code: "network.decoding_failed", httpStatus: payload.status))
            }
        }
    }

    private func postExpectingNoBody(path: String, body: some Encodable) async {
        _ = await send(path: path, body: body, useNoAuthSession: false, method: .post)
    }

    private func send(
        path: String,
        body: some Encodable,
        useNoAuthSession: Bool,
        method: HttpMethod
    ) async -> Result<(data: Data, status: Int), ApiError> {
        guard let url = URL(string: path, relativeTo: apiBaseURL) else {
            return .failure(ApiError(code: "network.invalid_url"))
        }
        var request = URLRequest(url: url)
        request.httpMethod = method.rawValue
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        do {
            request.httpBody = try encoder.encode(body)
        } catch {
            return .failure(ApiError(code: "network.encoding_failed"))
        }

        let accessToken = useNoAuthSession ? nil : tokenStore.current()?.accessToken
        headerAdapter.apply(to: &request, accessToken: accessToken)

        let session = useNoAuthSession ? noAuthSession : authedSession
        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                return .failure(ApiError(code: "network.no_response"))
            }
            guard (200 ..< 300).contains(http.statusCode) else {
                return .failure(decodeError(data: data, status: http.statusCode))
            }
            return .success((data, http.statusCode))
        } catch {
            return .failure(ApiError(code: "network.unreachable"))
        }
    }

    private func decodeError(data: Data, status: Int) -> ApiError {
        if let problem = try? decoder.decode(ProblemDetails.self, from: data) {
            return ApiError(code: problem.errorCode, message: problem.detail ?? problem.title, httpStatus: status)
        }
        return ApiError(httpStatus: status)
    }

    private func persist(_ dto: JwtTokenResponseDto, fallbackRefreshLifetime: RefreshLifetime) {
        guard let access = dto.token, !access.isEmpty else { return }
        let tokens = AuthTokens(
            accessToken: access,
            accessTokenExpiresAt: accessExpiry(from: access),
            refreshToken: dto.refreshToken ?? "",
            refreshTokenExpiresAt: refreshExpiry(from: dto.refreshTokenExpiresAt, lifetime: fallbackRefreshLifetime)
        )
        tokenStore.save(tokens)
    }

    private func accessExpiry(from jwt: String) -> Date {
        JwtDecoder.expiry(of: jwt) ?? Date().addingTimeInterval(15 * 60)
    }

    private func refreshExpiry(from iso: String?, lifetime: RefreshLifetime) -> Date {
        if let iso, let parsed = ISO8601DateParser.parse(iso) {
            return parsed
        }
        return Date().addingTimeInterval(lifetime.seconds)
    }

    private enum HttpMethod: String {
        case post = "POST"
        case put = "PUT"
    }

    private enum RefreshLifetime {
        case shortLived
        case longLived

        var seconds: TimeInterval {
            switch self {
            case .shortLived: 24 * 60 * 60
            case .longLived: 30 * 24 * 60 * 60
            }
        }
    }
}

struct EmptyResponse: Decodable {}

struct ProblemDetails: Decodable {
    let title: String?
    let detail: String?
    let errorCode: String?

    enum CodingKeys: String, CodingKey {
        case title
        case detail
        case errorCode
        case code
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        title = try container.decodeIfPresent(String.self, forKey: .title)
        detail = try container.decodeIfPresent(String.self, forKey: .detail)
        errorCode = try container.decodeIfPresent(String.self, forKey: .errorCode)
            ?? container.decodeIfPresent(String.self, forKey: .code)
    }
}

enum ISO8601DateParser {
    static func parse(_ value: String) -> Date? {
        let withFraction = ISO8601DateFormatter()
        withFraction.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = withFraction.date(from: value) { return date }

        let plain = ISO8601DateFormatter()
        plain.formatOptions = [.withInternetDateTime]
        return plain.date(from: value)
    }
}
