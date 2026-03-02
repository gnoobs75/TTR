import Foundation
import FoundationModels

// MARK: - Generable Types

@Generable
struct TurdBark {
    @Guide(description: "A short funny one-liner (under 40 chars) a cartoon turd character says during a sewer race. Poop puns encouraged.")
    var line: String
    @Guide(description: "The emotion behind the line", .anyOf(["excited", "scared", "cocky", "disgusted", "surprised", "relieved"]))
    var emotion: String
}

@Generable
struct DeathQuip {
    @Guide(description: "A funny death message (under 50 chars) for a cartoon turd who just crashed. Dark humor and poop puns. Reference the context.")
    var quip: String
}

@Generable
struct GraffitiText {
    @Guide(description: "Crude sewer wall graffiti (2-4 words per line, max 3 lines, separated by newlines). Toilet humor.")
    var text: String
    @Guide(description: "Spray paint style", .anyOf(["crude", "bold", "dripping", "stencil"]))
    var style: String
}

@Generable
struct RaceCommentary {
    @Guide(description: "Excited sports commentary (under 60 chars) about a sewer turd race. Over-the-top enthusiasm.")
    var line: String
    @Guide(description: "Energy level", .range(1...5))
    var energy: Int
}

// MARK: - Plugin Manager

@objc public class FoundationModelsPlugin: NSObject {

    private static var session: LanguageModelSession?
    private static let systemPrompt = """
    You are the voice of MrCorny, a corn-studded cartoon turd racing through sewer pipes. \
    Your humor is silly, punny, and toilet-themed. Keep responses SHORT. \
    Poop puns, sewer jokes, and bathroom humor are your specialty. \
    Never be mean-spirited. Always fun and goofy.
    """

    @objc public static func isAvailable() -> Bool {
        if #available(iOS 26.0, *) {
            return SystemLanguageModel.default.isAvailable
        }
        return false
    }

    @objc public static func initSession() {
        if #available(iOS 26.0, *) {
            session = LanguageModelSession(
                instructions: systemPrompt
            )
        }
    }

    // MARK: - Generation Methods

    @objc public static func generateBark(
        eventType: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("{\"line\":\"\",\"emotion\":\"\"}")
            return
        }

        Task {
            do {
                let prompt = "The player just experienced: \(eventType). Generate a reaction bark."
                let response = try await session.respond(
                    to: prompt,
                    generating: TurdBark.self
                )
                let json = "{\"line\":\"\(Self.escapeJSON(response.line))\",\"emotion\":\"\(response.emotion)\"}"
                DispatchQueue.main.async { callback(json) }
            } catch {
                DispatchQueue.main.async { callback("{\"line\":\"\",\"emotion\":\"\"}") }
            }
        }
    }

    @objc public static func generateDeathQuip(
        context: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("")
            return
        }

        Task {
            do {
                let prompt = "The turd just died: \(context). Write a funny death message."
                let response = try await session.respond(
                    to: prompt,
                    generating: DeathQuip.self
                )
                DispatchQueue.main.async { callback(response.quip) }
            } catch {
                DispatchQueue.main.async { callback("") }
            }
        }
    }

    @objc public static func generateGraffiti(
        zone: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("{\"text\":\"\",\"style\":\"crude\"}")
            return
        }

        Task {
            do {
                let prompt = "Write sewer wall graffiti for the \(zone) zone. 2-4 words per line, max 3 lines."
                let response = try await session.respond(
                    to: prompt,
                    generating: GraffitiText.self
                )
                let json = "{\"text\":\"\(Self.escapeJSON(response.text))\",\"style\":\"\(response.style)\"}"
                DispatchQueue.main.async { callback(json) }
            } catch {
                DispatchQueue.main.async { callback("{\"text\":\"\",\"style\":\"crude\"}") }
            }
        }
    }

    @objc public static func generateCommentary(
        raceState: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("{\"line\":\"\",\"energy\":3}")
            return
        }

        Task {
            do {
                let prompt = "Commentate this race moment: \(raceState)"
                let response = try await session.respond(
                    to: prompt,
                    generating: RaceCommentary.self
                )
                let json = "{\"line\":\"\(Self.escapeJSON(response.line))\",\"energy\":\(response.energy)}"
                DispatchQueue.main.async { callback(json) }
            } catch {
                DispatchQueue.main.async { callback("{\"line\":\"\",\"energy\":3}") }
            }
        }
    }

    // MARK: - Batch Generation

    @objc public static func generateBarkBatch(
        count: Int,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *) else {
            callback("[]")
            return
        }

        Task {
            // Fresh session per batch to avoid context buildup
            let batchSession = LanguageModelSession(instructions: systemPrompt)
            var results: [String] = []
            let events = ["near-miss dodge", "stomping an obstacle", "getting hit",
                          "speed boost", "high combo", "coin grab", "jumping",
                          "entering toxic zone", "close call", "racing overtake"]

            for i in 0..<count {
                let event = events[i % events.count]
                do {
                    let response = try await batchSession.respond(
                        to: "React to: \(event). Be unique, don't repeat.",
                        generating: TurdBark.self
                    )
                    let json = "{\"line\":\"\(Self.escapeJSON(response.line))\",\"emotion\":\"\(response.emotion)\"}"
                    results.append(json)
                } catch {
                    // Skip failures silently
                }
            }

            let arrayJSON = "[\(results.joined(separator: ","))]"
            DispatchQueue.main.async { callback(arrayJSON) }
        }
    }

    @objc public static func generateGraffitiBatch(
        count: Int,
        zones: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *) else {
            callback("[]")
            return
        }

        Task {
            let batchSession = LanguageModelSession(instructions: systemPrompt)
            var results: [String] = []
            let zoneList = zones.split(separator: ",").map(String.init)

            for i in 0..<count {
                let zone = zoneList[i % zoneList.count]
                do {
                    let response = try await batchSession.respond(
                        to: "Write unique sewer graffiti for the \(zone) zone. Short, crude, funny.",
                        generating: GraffitiText.self
                    )
                    let json = "{\"text\":\"\(Self.escapeJSON(response.text))\",\"style\":\"\(response.style)\"}"
                    results.append(json)
                } catch {
                    // Skip failures
                }
            }

            let arrayJSON = "[\(results.joined(separator: ","))]"
            DispatchQueue.main.async { callback(arrayJSON) }
        }
    }

    // MARK: - Helpers

    private static func escapeJSON(_ str: String) -> String {
        str.replacingOccurrences(of: "\\", with: "\\\\")
           .replacingOccurrences(of: "\"", with: "\\\"")
           .replacingOccurrences(of: "\n", with: "\\n")
           .replacingOccurrences(of: "\r", with: "")
           .replacingOccurrences(of: "\t", with: " ")
    }
}
