import Foundation

private final class SelfSignedTLSDelegate: NSObject, URLSessionDelegate, @unchecked Sendable {
    static let shared = SelfSignedTLSDelegate()

    func urlSession(
        _ session: URLSession,
        didReceive challenge: URLAuthenticationChallenge,
        completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void
    ) {
        if challenge.protectionSpace.host == "127.0.0.1",
           let trust = challenge.protectionSpace.serverTrust {
            completionHandler(.useCredential, URLCredential(trust: trust))
        } else {
            completionHandler(.performDefaultHandling, nil)
        }
    }
}

public final class QuotaClient: @unchecked Sendable {
    public static let shared = QuotaClient()

    private let session: URLSession
    private var cachedEndpoint: ServerEndpoint?

    public init() {
        let config = URLSessionConfiguration.ephemeral
        config.timeoutIntervalForRequest = 6.0
        config.timeoutIntervalForResource = 10.0
        self.session = URLSession(
            configuration: config,
            delegate: SelfSignedTLSDelegate.shared,
            delegateQueue: nil
        )
    }

    public static func makeRPCRequest(
        port: Int,
        useTLS: Bool,
        csrfToken: String,
        method: String,
        body: [String: Any]
    ) throws -> URLRequest {
        let scheme = useTLS ? "https" : "http"
        guard let url = URL(string: "\(scheme)://127.0.0.1:\(port)/exa.language_server_pb.LanguageServerService/\(method)") else {
            throw URLError(.badURL)
        }
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        req.setValue("1", forHTTPHeaderField: "Connect-Protocol-Version")
        req.setValue(csrfToken, forHTTPHeaderField: "x-codeium-csrf-token")
        req.httpBody = try JSONSerialization.data(withJSONObject: body)
        return req
    }

    private func callRPC(endpoint: ServerEndpoint, method: String, body: [String: Any]) async throws -> Data {
        var lastError: Error = URLError(.cannotConnectToHost)
        for port in endpoint.ports {
            for useTLS in [false, true] {
                do {
                    let req = try Self.makeRPCRequest(
                        port: port,
                        useTLS: useTLS,
                        csrfToken: endpoint.csrfToken,
                        method: method,
                        body: body
                    )
                    let (data, response) = try await session.data(for: req)
                    if let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) {
                        return data
                    }
                } catch {
                    lastError = error
                }
            }
        }
        throw lastError
    }

    public func fetchSnapshot(forceRefresh: Bool = true) async -> QuotaSnapshot? {
        var endpoint = cachedEndpoint ?? ServerDiscovery.discoverActiveServer()
        if endpoint == nil {
            return nil
        }

        do {
            let summaryData = try await callRPC(
                endpoint: endpoint!,
                method: "RetrieveUserQuotaSummary",
                body: ["forceRefresh": forceRefresh]
            )
            let groups = try QuotaParser.parseSummary(data: summaryData)

            let modelsData = try? await callRPC(
                endpoint: endpoint!,
                method: "GetCascadeModelConfigData",
                body: [:]
            )
            let models = (modelsData != nil) ? ((try? QuotaParser.parseModelConfigs(data: modelsData!)) ?? []) : []

            cachedEndpoint = endpoint
            return QuotaSnapshot(groups: groups, models: models, updatedAt: Date())
        } catch {
            // Re-discover in case language_server restarted on a new PID/port
            if let fresh = ServerDiscovery.discoverActiveServer(), fresh != endpoint {
                cachedEndpoint = fresh
                endpoint = fresh
                if let summaryData = try? await callRPC(
                    endpoint: fresh,
                    method: "RetrieveUserQuotaSummary",
                    body: ["forceRefresh": forceRefresh]
                ), let groups = try? QuotaParser.parseSummary(data: summaryData) {
                    let modelsData = try? await callRPC(
                        endpoint: fresh,
                        method: "GetCascadeModelConfigData",
                        body: [:]
                    )
                    let models = (modelsData != nil) ? ((try? QuotaParser.parseModelConfigs(data: modelsData!)) ?? []) : []
                    return QuotaSnapshot(groups: groups, models: models, updatedAt: Date())
                }
            }
            cachedEndpoint = nil
            return nil
        }
    }
}
